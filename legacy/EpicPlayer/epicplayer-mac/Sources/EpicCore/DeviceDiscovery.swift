import Foundation

/// Пакеты обнаружения устройств: UDP-запрос и TCP-объявление.
///
/// Формат проверен тестами против Tests/Fixtures/protocol-vectors.json.
public enum DeviceDiscovery {
    /// 3 байта: запрос по UDP. Отправляется на broadcast:333.
    public static let udpQuery: Data = Data([0x49, 0x00, 0x0D])

    /// Порт, который устройство использует для обратной связи.
    public static let tcpCallbackPort: UInt16 = 3331

    public static let tcpAnnouncementLength = 9

    /// Признак ответа устройства на запрос обнаружения.
    public static let responseMarker: UInt8 = 0x49

    /// 9 байт: объявление для обнаружения по TCP.
    ///
    /// Устройство читает адрес и порт, затем подключается TCP-клиентом.
    /// Ответ приложение отправляет на порт, с которого пришло соединение,
    /// а не на `tcpCallbackPort`.
    ///
    /// - Parameter localAddress: адрес компьютера, с которого
    ///   приложение принимает соединения.
    ///
    /// - Note: Windows-версия кладёт сюда адрес из `IPEndPoint`, который
    ///   у неё уже является broadcast-адресом (`x.x.x.255`), а не адресом
    ///   самого компьютера. Расхождение зафиксировано в протоколе как
    ///   `[проверить]`. Здесь используется настоящий адрес — при
    ///   подтверждении на устройстве это место нужно сверить.
    public static func encodeTCPAnnouncement(localAddress: IPv4Address) -> Data {
        var bytes: [UInt8] = [0x4C, 0x06]
        bytes.append(contentsOf: localAddress.octets)

        // 3331 = 0x0D03, little endian даёт 03 0D, затем терминатор 0D —
        // два одинаковых байта подряд.
        bytes.append(UInt8(truncatingIfNeeded: tcpCallbackPort))
        bytes.append(UInt8(truncatingIfNeeded: tcpCallbackPort >> 8))
        bytes.append(0x0D)

        precondition(bytes.count == tcpAnnouncementLength)
        return Data(bytes)
    }

    /// Разбор объявления — пригодится в тестах и при отладке на устройстве.
    public static func decodeTCPAnnouncement(_ data: Data) -> (address: IPv4Address, port: UInt16)? {
        let bytes = [UInt8](data)
        guard bytes.count >= tcpAnnouncementLength else { return nil }
        guard bytes[0] == 0x4C, bytes[1] == 0x06 else { return nil }

        let address = IPv4Address([bytes[2], bytes[3], bytes[4], bytes[5]])
        let port = UInt16(bytes[6]) | (UInt16(bytes[7]) << 8)
        return (address, port)
    }
}

/// Адрес IPv4 в виде четырёх октетов.
public struct IPv4Address: Hashable, Sendable, CustomStringConvertible {
    public let octets: [UInt8]

    public init(_ octets: [UInt8]) {
        precondition(octets.count == 4, "IPv4 address requires 4 octets")
        self.octets = octets
    }

    /// Байты в сетевом порядке — как `GetAddressBytes()` в .NET.
    public init?(b0: UInt8, _ b1: UInt8, _ b2: UInt8, _ b3: UInt8) {
        self.init([b0, b1, b2, b3])
    }

    /// Разбор строки вида `192.168.88.253`.
    ///
    /// Отличается от `IPv4Address("...")` в Foundation тем, что не
    /// различает IPv4 и IPv6 и не принимает нотацию с двоеточиями.
    public init?(dottedQuad: String) {
        let parts = dottedQuad.split(separator: ".", omittingEmptySubsequences: false)
        guard parts.count == 4 else { return nil }

        var octets: [UInt8] = []
        for part in parts {
            // Ведущие нули допустимы: 192.168.088.253 — тот же адрес.
            guard !part.isEmpty,
                  part.allSatisfy({ $0.isNumber }),
                  let value = UInt8(part),
                  value >= 0
            else { return nil }
            octets.append(value)
        }

        self.init(octets)
    }

    public var description: String {
        octets.map(String.init).joined(separator: ".")
    }
}