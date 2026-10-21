import Foundation

/// Команда воспроизведения, 8 байт, порт 333.
///
/// Формат зафиксирован в plans/protocol.md и проверен тестами против
/// Tests/Fixtures/protocol-vectors.json.
public enum PlaybackCommand: UInt8, CaseIterable, Sendable {
    case play = 0x02
    case pause = 0x03
    case stop = 0x04
    case nextTrack = 0x05
    case previousTrack = 0x06

    /// Разбор имени из эталонного файла тестов.
    init?(rawName: String) {
        switch rawName {
        case "play": self = .play
        case "pause": self = .pause
        case "stop": self = .stop
        case "nextTrack": self = .nextTrack
        case "previousTrack": self = .previousTrack
        default: return nil
        }
    }
}

public enum PlaybackPacket {
    public static let length = 8

    static let header: [UInt8] = [0x50, 0x05]
    static let terminator: UInt8 = 0x0D

    /// Позиция записывается по смещению 3.
    static let positionOffset = 3

    /// Собирает 8-байтовый пакет команды.
    ///
    /// - Parameter positionMilliseconds: позиция в миллисекундах.
    ///
    /// - Note: Каждый вызов возвращает новый буфер. Windows-версия
    ///   переиспользовала общий массив `_playBytes` из UI-потока и
    ///   фоновых таймеров одновременно — гонка приводила к отправке
    ///   перемешанных пакетов.
    public static func encode(
        _ command: PlaybackCommand,
        positionMilliseconds: UInt32 = 0
    ) -> Data {
        var bytes = header
        bytes.append(command.rawValue)

        // Младший байт первым. В .NET little endian получался как побочный
        // эффект архитектуры x86, здесь порядок задан явно: на big endian
        // машине иначе ушёл бы пакет с переставленными байтами.
        bytes.append(UInt8(truncatingIfNeeded: positionMilliseconds))
        bytes.append(UInt8(truncatingIfNeeded: positionMilliseconds >> 8))
        bytes.append(UInt8(truncatingIfNeeded: positionMilliseconds >> 16))
        bytes.append(UInt8(truncatingIfNeeded: positionMilliseconds >> 24))

        bytes.append(terminator)

        precondition(bytes.count == length)
        return Data(bytes)
    }

    /// Разбирает позицию из принятого пакета.
    ///
    /// Нужна для проверки ответов устройства: если оно возвращает
    /// состояние воспроизведения, позиция лежит на том же смещении.
    public static func position(from data: Data) -> UInt32? {
        let bytes = [UInt8](data)
        guard bytes.count >= length else { return nil }
        guard bytes[0] == header[0], bytes[1] == header[1] else { return nil }

        return UInt32(bytes[positionOffset])
            | (UInt32(bytes[positionOffset + 1]) << 8)
            | (UInt32(bytes[positionOffset + 2]) << 16)
            | (UInt32(bytes[positionOffset + 3]) << 24)
    }

    /// Команда из принятого пакета, если формат известен.
    public static func command(from data: Data) -> PlaybackCommand? {
        let bytes = [UInt8](data)
        guard bytes.count >= length else { return nil }
        guard bytes[0] == header[0], bytes[1] == header[1] else { return nil }
        return PlaybackCommand(rawValue: bytes[2])
    }
}