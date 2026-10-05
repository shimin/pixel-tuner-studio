import Foundation

/// Разбор входящих пакетов Art-Net.
///
/// Приложение только принимает Art-Net DMX (ArtDmx), остальные пакеты
/// не используются и не разбираются.
///
/// Порядок байтов в Art-Net смешанный, это соответствует спецификации:
/// заголовок, OpCode и версия — big endian, Universe — little endian.
public enum ArtNet {
    public static let port: UInt16 = 6454
    public static let headerSize = 12
}

public enum ArtNetOpCode: UInt16 {
    case none = 0x00
    case poll = 0x20
    case pollReply = 0x21
    case dmx = 0x50
    case todRequest = 0x80
    case todData = 0x81
    case todControl = 0x82
}

public struct ArtNetDMXPacket {
    public let opCode: ArtNetOpCode
    public let version: UInt16
    public let sequence: UInt8
    public let physical: UInt8
    public let universe: UInt16
    public let dmxData: Data
}

public enum ArtNetParser {
    /// Заголовок "Art-Net\0" — 8 байт.
    static let header: [UInt8] = [0x41, 0x72, 0x74, 0x2D, 0x4E, 0x65, 0x74, 0x00]

    /// Смещение имени устройства в ответе на запрос обнаружения.
    public static let deviceTagOffset = 47
    public static let deviceTagLength = 12

    /// Разбирает ArtDmx. Остальные пакеты возвращают nil: приложение
    /// их не использует.
    public static func parse(_ data: Data) -> ArtNetDMXPacket? {
        let bytes = [UInt8](data)

        // Заголовок 8 + OpCode 2 + версия 2 + Sequence 1 + Physical 1
        // + Universe 2 + Length 2 = 18 байт минимум.
        guard bytes.count >= 18 else { return nil }
        guard Array(bytes[0..<8]) == header else { return nil }

        let opCodeValue = (UInt16(bytes[8]) << 8) | UInt16(bytes[9])
        guard let opCode = ArtNetOpCode(rawValue: opCodeValue), opCode == .dmx else {
            return nil
        }

        // Версия — network order.
        let version = (UInt16(bytes[10]) << 8) | UInt16(bytes[11])

        let sequence = bytes[12]
        let physical = bytes[13]

        // Universe — little endian. В спецификации это SubUni/Net,
        // записанные в обратном порядке.
        let universe = UInt16(bytes[14]) | (UInt16(bytes[15]) << 8)

        // Length — big endian.
        let length = (UInt16(bytes[16]) << 8) | UInt16(bytes[17])
        let payloadLength = Int(min(length, UInt16(bytes.count - 18)))

        return ArtNetDMXPacket(
            opCode: opCode,
            version: version,
            sequence: sequence,
            physical: physical,
            universe: universe,
            dmxData: Data(bytes[18..<(18 + payloadLength)])
        )
    }

    /// Первый байт ответа устройства на запрос обнаружения.
    ///
    /// Windows-код проверяет `received[0] == 0x49 && received.Length > 3`,
    /// см. WifiDevicesWindow.xaml.cs:83.
    public static func isDeviceResponse(_ data: Data) -> Bool {
        let bytes = [UInt8](data)
        return bytes.count > 3 && bytes[0] == DeviceDiscovery.responseMarker
    }

    /// Разбор Windows-1251. Нули в хвосте отбрасываются.
    ///
    /// Windows-код: `Encoding.GetEncoding(1251).GetString(RawData, 47, 12)`,
    /// см. WifiInfoDevice.cs:51.
    public static func deviceTag(_ data: Data) -> String? {
        let bytes = [UInt8](data)
        guard bytes.count >= deviceTagOffset + deviceTagLength else { return nil }

        let tagBytes = bytes[deviceTagOffset..<(deviceTagOffset + deviceTagLength)]
        return decodeWindows1251(Data(tagBytes))
    }

    /// Разбор Windows-1251. Нули в хвосте отбрасываются.
    ///
    /// - Note: число 1251 в .NET — это код CP1251. В Darwin значение
    ///   1251 означает Latin-1, а кириллицы в нём нет. Правильная
    ///   константа CFStringEncoding для Windows-1251 — 11, что и есть
    ///   `String.Encoding.windowsCP1251` в Foundation.
    ///   Проверено на машине: байты слова «Привет» декодируются верно.
    static func decodeWindows1251(_ data: Data) -> String? {
        guard var decoded = String(data: data, encoding: .windowsCP1251) else {
            return nil
        }

        // Имя добито нулями, они обрезаются — как в Windows-коде.
        while decoded.hasSuffix("\0") {
            decoded.removeLast()
        }
        return decoded
    }
}

/// Пороги срабатывания Art-Net: значения канала, при которых
/// выполняется действие.
public struct ArtNetThresholds: Equatable {
    public var start: UInt8
    public var pause: UInt8
    public var stop: UInt8

    public init(start: UInt8, pause: UInt8, stop: UInt8) {
        self.start = start
        self.pause = pause
        self.stop = stop
    }

    /// Значения по умолчанию из настроек Windows-версии.
    public static let `default` = ArtNetThresholds(start: 50, pause: 100, stop: 150)
}

/// Действие, запрошенное пультом.
public enum DmxAction: Equatable {
    case play
    case pause
    case stop
}

/// Отслеживает значение DMX-канала и определяет действие.
///
/// Реакция происходит только при **изменении** значения: удержание
/// кнопки на пульте не перезапускает трек многократно. Это поведение
/// задано в Windows-версии, см. MainWindow.xaml.cs:621-636.
public struct DmxCommandTracker {
    /// Действие, ожидающее выполнения. `nil` — ничего не делать.
    public private(set) var pending: DmxAction?

    private var lastValue: UInt8 = 0
    private var hasLastValue = false

    public init() {}

    public mutating func update(value: UInt8, last: UInt8,
                                thresholds: ArtNetThresholds = .default) {
        hasLastValue = true
        lastValue = value

        // Первое значение всегда инициирует проверку: `last` передаётся
        // вызывающим кодом и в первый раз равен 0.
        if value == last {
            pending = nil
            return
        }

        if value == thresholds.start {
            pending = .play
        } else if value == thresholds.stop {
            pending = .stop
        } else if value == thresholds.pause {
            pending = .pause
        } else {
            pending = nil
        }
    }
}