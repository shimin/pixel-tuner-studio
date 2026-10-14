import Foundation

/// Пакет `ArtDmx` (OpCode `0x5000`). Остальные пакеты Art-Net
/// приложение не обрабатывает — см. plans/porting-plan.md.
public struct ArtDmxPacket: Equatable {
    public static let headerID: [UInt8] = Array("Art-Net".utf8) + [0]
    public static let opCode: UInt16 = 0x5000
    public static let headerLength = 18

    public let sequence: UInt8
    public let physical: UInt8
    /// 15-битный Port-Address: `Net << 8 | SubUni`.
    public let universe: UInt16
    /// Значения каналов; индекс 0 — первый канал DMX.
    public let data: [UInt8]

    public init(sequence: UInt8, physical: UInt8, universe: UInt16, data: [UInt8]) {
        self.sequence = sequence
        self.physical = physical
        self.universe = universe
        self.data = data
    }

    /// Разбор по спецификации Art-Net 4. Версия протокола не проверяется:
    /// старые пульты шлют меньше 14, а формат `ArtDmx` от этого не меняется.
    public static func parse(_ data: Data) -> ArtDmxPacket? {
        let bytes = [UInt8](data)
        guard bytes.count >= headerLength,
              Array(bytes[0..<8]) == headerID
        else { return nil }

        // OpCode — little endian, в отличие от остальных полей Art-Net.
        let opCode = UInt16(bytes[8]) | UInt16(bytes[9]) << 8
        guard opCode == Self.opCode else { return nil }

        // Длина — big endian.
        let length = Int(bytes[16]) << 8 | Int(bytes[17])
        guard length >= 1, length <= 512, bytes.count >= headerLength + length else { return nil }

        let subUni = UInt16(bytes[14])
        let net = UInt16(bytes[15] & 0x7F)

        return ArtDmxPacket(
            sequence: bytes[12],
            physical: bytes[13],
            universe: net << 8 | subUni,
            data: Array(bytes[headerLength..<(headerLength + length)])
        )
    }

    /// Сборка пакета — для тестов и отладки без пульта.
    public func encoded() -> Data {
        var bytes = Self.headerID
        bytes += [UInt8(truncatingIfNeeded: Self.opCode), UInt8(truncatingIfNeeded: Self.opCode >> 8)]
        bytes += [0, 14]
        bytes += [sequence, physical]
        bytes += [UInt8(truncatingIfNeeded: universe), UInt8(truncatingIfNeeded: universe >> 8) & 0x7F]
        bytes += [UInt8(truncatingIfNeeded: data.count >> 8), UInt8(truncatingIfNeeded: data.count)]
        bytes += data
        return Data(bytes)
    }
}

/// Настройки реакции на DMX. Значения по умолчанию — как в Windows-версии.
public struct DmxTriggerSettings: Equatable {
    /// Индекс в массиве DMX, с нуля — как `DmxData[ArtnetChannel]`
    /// в Windows-версии. 0 — первый канал.
    public var channel: Int
    public var playValue: UInt8
    public var pauseValue: UInt8
    public var stopValue: UInt8
    /// `nil` — любой universe. Windows-версия хранит настройку Universe,
    /// но не применяет её.
    public var universe: UInt16?

    public init(channel: Int = 0, playValue: UInt8 = 50, pauseValue: UInt8 = 100, stopValue: UInt8 = 150, universe: UInt16? = nil) {
        self.channel = channel
        self.playValue = playValue
        self.pauseValue = pauseValue
        self.stopValue = stopValue
        self.universe = universe
    }
}

/// Превращает поток DMX в команды. Срабатывает на **изменение** значения
/// канала: удержание фейдера на 50 не перезапускает трек.
public struct DmxTrigger {
    public var settings: DmxTriggerSettings

    /// Начальное 0 — как `_lastCmd` в Windows-версии.
    private var lastValue: UInt8 = 0

    public init(settings: DmxTriggerSettings = DmxTriggerSettings()) {
        self.settings = settings
    }

    public mutating func process(_ packet: ArtDmxPacket) -> PlaybackCommand? {
        if let universe = settings.universe, universe != packet.universe { return nil }
        guard settings.channel >= 0, settings.channel < packet.data.count else { return nil }

        let value = packet.data[settings.channel]
        defer { lastValue = value }
        guard value != lastValue else { return nil }

        switch value {
        case settings.playValue: return .play
        case settings.stopValue: return .stop
        case settings.pauseValue: return .pause
        default: return nil
        }
    }
}
