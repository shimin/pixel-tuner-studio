import Foundation

extension IPv4Address {
    /// `0.0.0.0` — привязка сокета ко всем адресам.
    public static let any = IPv4Address([0, 0, 0, 0])

    /// Адрес как число в порядке хоста: `192.168.88.253` → `0xC0A858FD`.
    public var hostOrderValue: UInt32 {
        octets.reduce(0) { $0 << 8 | UInt32($1) }
    }

    public init(hostOrderValue value: UInt32) {
        self.init([
            UInt8(truncatingIfNeeded: value >> 24),
            UInt8(truncatingIfNeeded: value >> 16),
            UInt8(truncatingIfNeeded: value >> 8),
            UInt8(truncatingIfNeeded: value),
        ])
    }

    /// Broadcast-адрес подсети по маске: `адрес | ~маска`.
    ///
    /// Для /24 совпадает с поведением Windows-версии, для остальных масок
    /// исправляет расхождение 2 из plans/protocol.md.
    public func broadcast(netmask: IPv4Address) -> IPv4Address {
        IPv4Address(hostOrderValue: hostOrderValue | ~netmask.hostOrderValue)
    }

    /// Broadcast так, как его считает Windows-версия: последний октет
    /// заменяется на 255, маска игнорируется (`MainWindow.xaml.cs:200`).
    ///
    /// Не для отправки — только для сравнения и проверки на устройстве.
    public var legacyBroadcast: IPv4Address {
        IPv4Address([octets[0], octets[1], octets[2], 255])
    }

    /// Длина префикса маски: `255.255.255.0` → 24.
    public var prefixLength: Int {
        hostOrderValue.nonzeroBitCount
    }
}
