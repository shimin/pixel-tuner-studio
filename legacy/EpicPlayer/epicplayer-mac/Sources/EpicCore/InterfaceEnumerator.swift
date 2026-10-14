import Darwin
import Foundation

/// Тип интерфейса по данным `Network.framework`.
public enum InterfaceKind: String, Sendable {
    case wiredEthernet
    case wifi
    case other
    /// `NWPathMonitor` не сообщил тип — интерфейс не отбрасывается.
    case unknown
}

/// IPv4-адрес на сетевом интерфейсе. Одна запись на адрес: если на
/// интерфейсе их несколько, будет несколько записей.
public struct NetworkInterface: Hashable, Identifiable, Sendable {
    /// Имя BSD: `en0`, `en7`.
    public let name: String
    /// Индекс для `IP_BOUND_IF`.
    public let index: UInt32
    public let address: IPv4Address
    public let netmask: IPv4Address
    public var kind: InterfaceKind

    public var id: String { "\(name)/\(address)" }

    public var broadcastAddress: IPv4Address {
        address.broadcast(netmask: netmask)
    }

    /// Для списка выбора: `en0 · Wi-Fi · 192.168.88.10/24`.
    public var displayName: String {
        let kindName: String
        switch kind {
        case .wiredEthernet: kindName = "Ethernet"
        case .wifi: kindName = "Wi-Fi"
        case .other: kindName = "другой"
        case .unknown: kindName = "?"
        }
        return "\(name) · \(kindName) · \(address)/\(netmask.prefixLength)"
    }

    public init(name: String, index: UInt32, address: IPv4Address, netmask: IPv4Address, kind: InterfaceKind) {
        self.name = name
        self.index = index
        self.address = address
        self.netmask = netmask
        self.kind = kind
    }
}

/// Список интерфейсов, пригодных для связи с устройством.
///
/// Замена фильтра `Ethernet || Wireless80211` Windows-версии, который
/// на macOS не работает (подводный камень 2 в plans/porting-plan.md).
public enum InterfaceEnumerator {
    /// Интерфейсы для выбора пользователем, с типом из `NWPathMonitor`.
    ///
    /// Блокирует поток до получения типов (обычно миллисекунды, не
    /// более `timeout`). Не вызывать на главном потоке в UI.
    public static func available(timeout: TimeInterval = 1) -> [NetworkInterface] {
        let kinds = InterfaceTypeProbe.currentKinds(timeout: timeout)
        do {
            return try ipv4Interfaces(kinds: kinds)
        } catch {
            Log.network.error("getifaddrs: \(String(describing: error), privacy: .public)")
            return []
        }
    }

    /// - Parameters:
    ///   - kinds: тип по имени интерфейса; пустой словарь — типы неизвестны.
    ///   - includeVirtual: не применять фильтр — для диагностики.
    public static func ipv4Interfaces(
        kinds: [String: InterfaceKind] = [:],
        includeVirtual: Bool = false
    ) throws -> [NetworkInterface] {
        var head: UnsafeMutablePointer<ifaddrs>?
        guard getifaddrs(&head) == 0, let first = head else {
            throw SocketError.last("getifaddrs")
        }
        defer { freeifaddrs(head) }

        var result: [NetworkInterface] = []
        for pointer in sequence(first: first, next: { $0.pointee.ifa_next }) {
            let entry = pointer.pointee
            guard let addressPointer = entry.ifa_addr,
                  addressPointer.pointee.sa_family == sa_family_t(AF_INET),
                  let maskPointer = entry.ifa_netmask
            else { continue }

            let name = String(cString: entry.ifa_name)
            let kind = kinds[name] ?? .unknown

            if !includeVirtual {
                guard isCandidate(name: name, flags: entry.ifa_flags, kind: kind) else { continue }
            }

            let address = ipv4(from: addressPointer)
            let netmask = ipv4(from: maskPointer)
            let index = if_nametoindex(entry.ifa_name)
            guard index != 0 else { continue }

            result.append(NetworkInterface(name: name, index: index, address: address, netmask: netmask, kind: kind))
        }

        return result.sorted(by: sortOrder)
    }

    /// Префиксы виртуальных интерфейсов macOS. Ни один из них не ведёт
    /// к устройству шоу, но часть из них поднята и имеет IPv4.
    static let virtualPrefixes = [
        "lo",       // loopback
        "utun",     // VPN, iCloud Private Relay
        "ipsec",    // VPN
        "awdl",     // AirDrop
        "llw",      // low latency WLAN (AirDrop)
        "ap",       // точка доступа / Internet Sharing
        "anpi",     // внутренние интерфейсы Apple Silicon
        "bridge",   // Thunderbolt Bridge, сети виртуальных машин
        "gif",      // туннели
        "stf",      // 6to4
        "vmenet",   // виртуализация
        "feth",     // фейковые ethernet-пары
    ]

    /// Чистая функция фильтра — проверяется тестами без сети.
    static func isCandidate(name: String, flags: UInt32, kind: InterfaceKind) -> Bool {
        let required = UInt32(IFF_UP | IFF_RUNNING | IFF_BROADCAST)
        guard flags & required == required else { return false }
        guard flags & UInt32(IFF_LOOPBACK | IFF_POINTOPOINT) == 0 else { return false }
        guard !virtualPrefixes.contains(where: { hasPrefix(name, $0) }) else { return false }

        // Тип известен и это не Ethernet/Wi-Fi — отбрасываем. Неизвестный
        // тип оставляем: USB-адаптер с самоназначенным адресом может не
        // попасть в NWPath. [проверить] на реальной машине.
        return kind != .other
    }

    /// `en0` не должно совпасть с префиксом `e`, а `ap1` — с `ap`.
    /// Префикс засчитывается, только если за ним идёт цифра.
    private static func hasPrefix(_ name: String, _ prefix: String) -> Bool {
        guard name.hasPrefix(prefix) else { return false }
        let rest = name.dropFirst(prefix.count)
        return rest.isEmpty || rest.first!.isNumber
    }

    private static func ipv4(from pointer: UnsafeMutablePointer<sockaddr>) -> IPv4Address {
        pointer.withMemoryRebound(to: sockaddr_in.self, capacity: 1) {
            IPv4Address(hostOrderValue: UInt32(bigEndian: $0.pointee.sin_addr.s_addr))
        }
    }

    private static func sortOrder(_ lhs: NetworkInterface, _ rhs: NetworkInterface) -> Bool {
        func rank(_ kind: InterfaceKind) -> Int {
            switch kind {
            case .wiredEthernet: return 0
            case .wifi: return 1
            case .unknown: return 2
            case .other: return 3
            }
        }
        if rank(lhs.kind) != rank(rhs.kind) { return rank(lhs.kind) < rank(rhs.kind) }
        return lhs.name.localizedStandardCompare(rhs.name) == .orderedAscending
    }
}
