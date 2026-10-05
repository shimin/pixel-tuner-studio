import Foundation

/// Маска подсети IPv4.
public struct SubnetMask: Equatable {
    public let octets: [UInt8]

    public init(_ octets: [UInt8]) {
        precondition(octets.count == 4, "Subnet mask requires 4 octets")
        self.octets = octets
    }

    public init?(dottedQuad: String) {
        guard let address = IPv4Address(dottedQuad: dottedQuad) else { return nil }
        self.init(address.octets)
    }

    /// Разбор маски из строки, как её вводит пользователь в настройках
    /// Art-Net.
    public init?(_ dottedQuad: String) {
        self.init(dottedQuad: dottedQuad)
    }

    /// Маска должна идти непрерывно: 255.255.0.1 недопустима.
    public var isValid: Bool {
        // Разворачиваем в битовую последовательность длиной 32 и
        // проверяем, что единицы идут до первого нуля.
        var sawZero = false
        for octet in octets {
            for bit in stride(from: 7, through: 0, by: -1) {
                if (octet >> UInt8(bit)) & 1 == 1 {
                    if sawZero { return false }
                } else {
                    sawZero = true
                }
            }
        }
        return true
    }
}

public extension IPv4Address {
    /// Broadcast-адрес: адрес ИЛИ инверсия маски.
    ///
    /// Windows-версия заменяет последний октет на 255, что верно только
    /// для /24. Здесь считается по маске.
    func broadcastAddress(mask: SubnetMask) -> IPv4Address? {
        guard mask.isValid else { return nil }

        // /31 и /32 не имеют широковещательного адреса.
        var hostBits = 0
        for octet in mask.octets {
            hostBits += 8 - octet.nonzeroBitCount
        }
        guard hostBits >= 2 else { return nil }

        var result: [UInt8] = []
        for i in 0..<4 {
            result.append(octets[i] | (~mask.octets[i] & 0xFF))
        }
        return IPv4Address(result)
    }
}

/// Тип интерфейса в терминах, понятных приложению.
public enum InterfaceKind {
    case ethernet
    case tunnel
    case bridge
    case loopback
    case other
}

/// Сетевой интерфейс, пригодный для связи с устройством шоу.
public struct NetworkInterface: Identifiable {
    public let name: String
    /// Индекс для привязки сокета. На macOS без него broadcast уходит
    /// по маршруту по умолчанию и может уйти в utun.
    public let index: Int32
    public let addresses: [IPv4Address]
    public let netmask: SubnetMask?
    public let kind: InterfaceKind

    public var id: String { name }

    /// Первый адрес, для которого вычисляется широковещательный.
    public var primaryAddress: IPv4Address? {
        addresses.first
    }

    /// Broadcast-адрес интерфейса.
    ///
    /// - Note: на macOS у интерфейсов часто несколько адресов. Здесь
    ///   берётся первый IPv4; при необходимости выбор можно уточнить
    ///   после проверки на реальном устройстве.
    public var broadcastAddress: IPv4Address? {
        guard let netmask = netmask, let address = primaryAddress else { return nil }
        return address.broadcastAddress(mask: netmask)
    }

    /// Текст для выпадающего списка: имя интерфейса и адрес.
    public var displayName: String {
        guard let address = primaryAddress else { return name }
        return "\(name) | \(address)"
    }

    public init(name: String, index: Int32, addresses: [IPv4Address],
                netmask: SubnetMask?, kind: InterfaceKind) {
        self.name = name
        self.index = index
        self.addresses = addresses
        self.netmask = netmask
        self.kind = kind
    }
}

public enum InterfaceFilter {
    /// Оставляет интерфейсы, с которыми реально можно связаться:
    /// Ethernet или Wi-Fi, с адресом IPv4 и корректной маской.
    ///
    /// На macOS в списке всегда есть виртуальные интерфейсы — utun, awdl,
    /// Thunderbolt Bridge, bridge. Windows-версия отбирала только
    /// Ethernet и Wireless80211, но на macOS Wi-Fi (en0) определяется
    /// как Ethernet, а utun попадает в тот же список.
    public static func usable(_ interfaces: [NetworkInterface]) -> [NetworkInterface] {
        interfaces.filter { iface in
            guard iface.kind == .ethernet else { return false }
            guard iface.primaryAddress != nil else { return false }
            guard let netmask = iface.netmask, netmask.isValid else { return false }
            return iface.broadcastAddress != nil
        }
    }
}