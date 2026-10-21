import XCTest
@testable import EpicCore

/// Проверяет, что реализация на macOS собирает ровно те байты,
/// которые ожидает протокол.
///
/// Эталоны лежат в Tests/Fixtures/protocol-vectors.json — общем файле
/// с Windows-тестами. Файл заполнен значениями из исходного C#-кода,
/// поэтому расхождение здесь означает расхождение протокола.
final class ProtocolVectorTests: XCTestCase {

    // MARK: - Загрузка эталонов

    private struct Fixture: Decodable {
        struct Playback: Decodable {
            let name: String
            let command: String
            let positionMilliseconds: UInt32
            let expected: String
        }
        struct UdpQuery: Decodable {
            let name: String
            let expected: String
        }
        struct TcpAnnouncement: Decodable {
            let name: String
            let address: String
            let expected: String
        }
        let playbackCommand: [Playback]
        let udpQuery: [UdpQuery]
        let tcpAnnouncement: [TcpAnnouncement]
    }

    private func loadFixture() throws -> Fixture {
        let url = Self.fixtureURL
        let data = try Data(contentsOf: url)
        return try JSONDecoder().decode(Fixture.self, from: data)
    }

    /// Путь считается от исходника теста, чтобы не зависеть от
    /// рабочего каталога при запуске.
    private static var fixtureURL: URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()   // EpicCoreTests
            .deletingLastPathComponent()   // Tests
            .appendingPathComponent("Fixtures/protocol-vectors.json")
    }

    private func bytes(from hex: String) -> [UInt8] {
        hex.split(separator: " ")
            .map { UInt8($0, radix: 16)! }
    }

    private func hex(from data: Data) -> String {
        data.map { String(format: "%02X", $0) }.joined(separator: " ")
    }

    // MARK: - Команда воспроизведения

    func testPlaybackCommandsMatchVectors() throws {
        let fixture = try loadFixture()
        XCTAssertFalse(fixture.playbackCommand.isEmpty, "Эталоны не загрузились")

        for vector in fixture.playbackCommand {
            guard let command = PlaybackCommand(rawName: vector.command) else {
                XCTFail("Неизвестная команда в эталоне: \(vector.command)")
                continue
            }

            let actual = PlaybackPacket.encode(
                command,
                positionMilliseconds: vector.positionMilliseconds
            )

            XCTAssertEqual(
                hex(from: actual),
                vector.expected,
                "Расхождение с протоколом: \(vector.name)"
            )
        }
    }

    /// Длина пакета фиксирована протоколом и не зависит от команды
    /// и позиции.
    func testPlaybackPacketIsAlwaysEightBytes() {
        for command in PlaybackCommand.allCases {
            for position: UInt32 in [0, 1, 1000, 65535, 65536, 4294967295] {
                let data = PlaybackPacket.encode(command, positionMilliseconds: position)
                XCTAssertEqual(data.count, 8)
            }
        }
    }

    /// Заголовок и терминатор не зависят от команды.
    func testHeaderAndTerminatorAreFixed() {
        for command in PlaybackCommand.allCases {
            let bytes = [UInt8](PlaybackPacket.encode(command, positionMilliseconds: 0))
            XCTAssertEqual(bytes[0], 0x50, "Заголовок должен быть 0x50")
            XCTAssertEqual(bytes[1], 0x05, "Заголовок должен быть 0x05")
            XCTAssertEqual(bytes[7], 0x0D, "Терминатор должен быть 0x0D")
            XCTAssertEqual(bytes[2], command.rawValue)
        }
    }

    /// Коды команд обязаны совпадать с Windows-версией, иначе устройство
    /// получит неверную команду.
    func testCommandCodesMatchWindows() {
        XCTAssertEqual(PlaybackCommand.play.rawValue, 0x02)
        XCTAssertEqual(PlaybackCommand.pause.rawValue, 0x03)
        XCTAssertEqual(PlaybackCommand.stop.rawValue, 0x04)
        XCTAssertEqual(PlaybackCommand.nextTrack.rawValue, 0x05)
        XCTAssertEqual(PlaybackCommand.previousTrack.rawValue, 0x06)
    }

    /// Порядок байтов little endian. В .NET он получался как побочный
    /// эффект архитектуры x86; здесь задан явно.
    ///
    /// Значение 0x01000000 в big endian дало бы 01 00 00 00 —
    /// такой пакет устройство поняло бы как позицию 16 777 216,
    /// то есть неверную.
    func testPositionIsLittleEndian() {
        let bytes = [UInt8](
            PlaybackPacket.encode(.play, positionMilliseconds: 0x0100_0000)
        )
        XCTAssertEqual(bytes[3], 0x00, "Младший байт позиции идёт первым")
        XCTAssertEqual(bytes[4], 0x00)
        XCTAssertEqual(bytes[5], 0x00)
        XCTAssertEqual(bytes[6], 0x01, "Старший байт позиции идёт последним")
    }

    /// Каждая сборка возвращает независимый буфер: исходный код Windows
    /// переиспользовал общий массив, из-за чего пакеты могли перемешиваться
    /// при отправке из разных потоков.
    func testEachCallReturnsIndependentBuffer() {
        let first = PlaybackPacket.encode(.play, positionMilliseconds: 1)
        let second = PlaybackPacket.encode(.stop, positionMilliseconds: 2)

        XCTAssertEqual(first.count, 8)
        XCTAssertEqual(second.count, 8)
        XCTAssertNotEqual(first, second, "Буферы не должны быть общими")

        // Первая команда не должна измениться после второй сборки.
        XCTAssertEqual([UInt8](first)[2], PlaybackCommand.play.rawValue)
        XCTAssertEqual([UInt8](first)[3], 1)
    }

    // MARK: - Device lookup, UDP

    func testUdpQueryMatchesVector() throws {
        let fixture = try loadFixture()
        for vector in fixture.udpQuery {
            XCTAssertEqual(hex(from: DeviceDiscovery.udpQuery), vector.expected, vector.name)
        }
    }

    // MARK: - Device lookup, TCP

    func testTCPAnnouncementMatchesVectors() throws {
        let fixture = try loadFixture()
        XCTAssertFalse(fixture.tcpAnnouncement.isEmpty, "Эталоны не загрузились")

        for vector in fixture.tcpAnnouncement {
            guard let address = IPv4Address(dottedQuad: vector.address) else {
                XCTFail("Некорректный адрес в эталоне: \(vector.address)")
                continue
            }

            let actual = DeviceDiscovery.encodeTCPAnnouncement(localAddress: address)
            XCTAssertEqual(hex(from: actual), vector.expected, "Расхождение: \(vector.name)")
        }
    }

    func testTCPAnnouncementIsNineBytes() {
        let address = IPv4Address([192, 168, 88, 253])
        let data = DeviceDiscovery.encodeTCPAnnouncement(localAddress: address)
        XCTAssertEqual(data.count, 9)
    }

    /// Порт 3331 = 0x0D03. В little endian это 03 0D, затем терминатор 0D.
    /// Ошибка порядка здесь дала бы 0D 03 0D и устройство подключилось бы
    /// не на тот порт.
    func testTCPCallbackPortIsLittleEndian() {
        let address = IPv4Address([192, 168, 88, 253])
        let bytes = [UInt8](DeviceDiscovery.encodeTCPAnnouncement(localAddress: address))

        XCTAssertEqual(DeviceDiscovery.tcpCallbackPort, 3331)
        XCTAssertEqual(bytes[6], 0x03, "Младший байт порта первым")
        XCTAssertEqual(bytes[7], 0x0D, "Затем старший байт порта")
        XCTAssertEqual(bytes[8], 0x0D, "Терминатор")
    }
}