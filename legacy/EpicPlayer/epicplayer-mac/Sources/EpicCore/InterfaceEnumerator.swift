import Darwin
import Foundation

/// Перечисление реальных сетевых интерфейсов системы.
///
/// Windows-версия брала `NetworkInterface.GetAllNetworkInterfaces()` и
/// оставляла Ethernet и Wireless80211. На macOS так не выйдет: Wi-Fi
/// (`en0`) определяется как Ethernet, а в списке всегда есть виртуальные
/// интерфейсы — `utun*`, `awdl*`, Thunderbolt Bridge, `bridge*`, которые
/// для связи с устройством шоу непригодны.
public enum InterfaceEnumerator {

    /// Все интерфейсы, пригодные для работы с устройством.
    public static func usableInterfaces() -> [NetworkInterface] {
        InterfaceFilter.usable(allInterfaces())
    }

    /// Все интерфейсы системы, включая виртуальные и loopback.
    public static func allInterfaces() -> [NetworkInterface] {
        var head: UnsafeMutablePointer<ifaddrs>?
        guard getifaddrs(&head) == 0, let first = head else { return [] }
        defer { freeifaddrs(head) }

        var result: [NetworkInterface] = []
        var cursor: UnsafeMutablePointer<ifaddrs>? = first

        while let entry = cursor {
            let interface = entry.pointee

            // Интересуют только IPv4: протокол и Art-Net работают по IPv4.
            guard interface.ifa_addr != nil else {
                cursor = interface.ifa_next
                continue
            }

            let name = String(cString: interface.ifa_name)
            // Индекс имён��тся через if_nametoindex: в Swift поля
            // ifa_ifindex у структуры ifaddrs нет.
            let index = if_nametoindex(name)

            guard let address = ipv4Address(from: interface.ifa_addr) else {
                cursor = interface.ifa_next
                continue
            }

            let netmask = self.netmask(from: interface.ifa_netmask)

            // У одного интерфейса бывает несколько адресов — наполняем
            // уже созданную запись либо создаём новую.
            if let existing = result.firstIndex(where: { $0.name == name }) {
                result[existing].addresses.append(address)
                if result[existing].netmask == nil {
                    result[existing].netmask = netmask
                }
            } else {
                result.append(NetworkInterface(
                    name: name,
                    index: Int32(index),
                    addresses: [address],
                    netmask: netmask,
                    kind: kind(for: name)))
            }

            cursor = interface.ifa_next
        }

        return result
    }

    /// Извлекает IPv4-адрес из `sockaddr`. Другие семейства отбрасываются.
    private static func ipv4Address(from pointer: UnsafePointer<sockaddr>?) -> IPv4Address? {
        guard let pointer = pointer else { return nil }
        guard pointer.pointee.sa_family == UInt8(AF_INET) else { return nil }

        var value = sockaddr_in()
        withUnsafePointer(to: pointer) { source in
            source.withMemoryRebound(to: sockaddr_in.self, capacity: 1) {
                value = $0.pointee
            }
        }
        return IPv4Address(inAddrT: value.sin_addr.s_addr)
    }

    /// Маска подсети интерфейса из `ifa_netmask`.
    private static func netmask(from pointer: UnsafePointer<sockaddr>?) -> SubnetMask? {
        guard let address = ipv4Address(from: pointer) else { return nil }
        return SubnetMask(address.octets)
    }

    /// Тип интерфейса по имени.
    ///
    /// Имена на macOS предсказуемы: `en*` — Ethernet или Wi-Fi,
    /// `utun*` и `ipsec*` — туннели, `bridge*` — мост, `lo*` — loopback.
    /// Wi-Fi от проводного Ethernet по имени не отличить, но для задачи
    /// это неважно: оба подходят.
    static func kind(for name: String) -> InterfaceKind {
        if name.hasPrefix("lo") { return .loopback }
        if name.hasPrefix("utun") || name.hasPrefix("ipsec")
            || name.hasPrefix("ppp") || name.hasPrefix("gif") {
            return .tunnel
        }
        if name.hasPrefix("bridge") { return .bridge }
        if name.hasPrefix("en") { return .ethernet }
        return .other
    }
}