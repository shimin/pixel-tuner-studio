import Foundation

/// Ответ устройства на UDP-запрос обнаружения.
///
/// Формат известен частично (раздел 5 plans/protocol.md): маркер
/// `0x49`, длина больше 3 байт, имя в байтах 47–58 в Windows-1251.
/// Остальное хранится сырым для разбора на устройстве.
public struct DeviceResponse: Equatable, Sendable {
    /// Байты имени, как `GetString(RawData, 47, 12)` в `WifiInfoDevice.cs:51`.
    public static let nameRange = 47..<59

    public let address: IPv4Address
    public let raw: Data

    /// `nil`, если это не ответ устройства. Собственный 3-байтовый запрос
    /// `49 00 0D`, вернувшийся через broadcast, отсекается по длине —
    /// так же, как в Windows-версии.
    public init?(data: Data, sender: IPv4Address) {
        guard data.count > DeviceDiscovery.udpQuery.count,
              data.first == DeviceDiscovery.responseMarker
        else { return nil }

        self.address = sender
        self.raw = data
    }

    /// Имя устройства. `nil`, если ответ короче 59 байт — Windows-версия
    /// в этом случае бросала исключение.
    public var name: String? {
        let bytes = [UInt8](raw)
        guard bytes.count >= Self.nameRange.upperBound else { return nil }

        var nameBytes = Array(bytes[Self.nameRange])
        while nameBytes.last == 0 {
            nameBytes.removeLast()
        }
        return String(bytes: nameBytes, encoding: .windowsCP1251)
    }

    public var rawHex: String {
        raw.hexString
    }
}
