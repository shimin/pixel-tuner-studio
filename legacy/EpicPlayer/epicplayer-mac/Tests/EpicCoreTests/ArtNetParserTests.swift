import XCTest
@testable import EpicCore

/// Разбор входящего Art-Net пакета.
///
/// Приложение только принимает ArtDmx, остальные пакеты не используются.
/// Эталоны — Tests/Fixtures/artnet-vectors.json.
final class ArtNetParserTests: XCTestCase {

    // MARK: - Загрузка эталонов

    private struct Fixture: Decodable {
        struct Dmx: Decodable {
            let name: String
            let universe: UInt16
            let sequence: UInt8
            let physical: UInt8
            let dmxValues: [UInt8]?
            let dmxLong: Bool?
            let dmxCount: Int?
            let expectedHex: String?
            let expectedPrefix: String?
        }
        struct DeviceResponse: Decodable {
            let name: String
            let hex: String
            let isDevice: Bool
            let tag: String?
        }
        let artDmx: [Dmx]
        let deviceResponse: [DeviceResponse]
    }

    private func loadFixture() throws -> Fixture {
        let url = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Fixtures/artnet-vectors.json")
        return try JSONDecoder().decode(Fixture.self, from: Data(contentsOf: url))
    }

    private func bytes(from hex: String) -> [UInt8] {
        hex.split(separator: " ").map { UInt8($0, radix: 16)! }
    }

    private func hex(from data: Data) -> String {
        data.map { String(format: "%02X", $0) }.joined(separator: " ")
    }

    // MARK: - Разбор ArtDmx

    func testArtDmxParsesVectors() throws {
        let fixture = try loadFixture()
        XCTAssertFalse(fixture.artDmx.isEmpty, "Эталоны не загрузились")

        for vector in fixture.artDmx {
            let data: Data
            if let expectedHex = vector.expectedHex {
                data = Data(bytes(from: expectedHex))
            } else if let prefix = vector.expectedPrefix {
                // Пакет с 512 каналами: префикс проверяем отдельно,
                // длину и хвост — по разбору.
                let header = bytes(from: prefix)
                let values = (0..<512).map { _ in UInt8(0) }
                data = Data(header + values)
            } else {
                XCTFail("В эталоне нет ожидаемых байтов: \(vector.name)")
                continue
            }

            guard let packet = ArtNetParser.parse(data) else {
                XCTFail("Пакет не распознан: \(vector.name)")
                continue
            }

            XCTAssertEqual(packet.universe, vector.universe, "universe: \(vector.name)")
            XCTAssertEqual(packet.sequence, vector.sequence, "sequence: \(vector.name)")
            XCTAssertEqual(packet.physical, vector.physical, "physical: \(vector.name)")

            if let values = vector.dmxValues {
                XCTAssertEqual([UInt8](packet.dmxData), values, "dmx: \(vector.name)")
            }
        }
    }

    /// Заголовок Art-Net: 8 байт "Art-Net\0", затем OpCode и версия
    /// в network order (big endian).
    func testHeaderIsParsed() {
        let data = Data(bytes(from:
            "41 72 74 2D 4E 65 74 00 00 50 00 0E 00 00 01 00 00 01 FF"))
        guard let packet = ArtNetParser.parse(data) else {
            XCTFail("Пакет не распознан")
            return
        }
        XCTAssertEqual(packet.opCode, .dmx)
    }

    /// OpCode 0x50 читается как big endian. При little endian получилось бы
    /// 0x5000, и пакет не распознался бы.
    func testOpCodeIsBigEndian() {
        // Байты 8-9 = 00 50 => 0x0050 при big endian.
        let data = Data(bytes(from:
            "41 72 74 2D 4E 65 74 00 00 50 00 0E 00 00 01 00 00 01 FF"))
        XCTAssertNotNil(ArtNetParser.parse(data))
    }

    /// Universe — little endian, в отличие от OpCode. Universe 256
    /// даёт 00 01; при big endian было бы 01 00, то есть 1 вместо 256.
    func testUniverseIsLittleEndian() {
        let data = Data(bytes(from:
            "41 72 74 2D 4E 65 74 00 00 50 00 0E FF 01 00 01 00 00 02 01 02"))
        guard let packet = ArtNetParser.parse(data) else {
            XCTFail("Пакет не распознан")
            return
        }
        XCTAssertEqual(packet.universe, 256)
    }

    /// Length — big endian. Значение 512 = 0x0200.
    func testLengthIsBigEndian() {
        var bytes: [UInt8] = bytes(from:
            "41 72 74 2D 4E 65 74 00 00 50 00 0E 07 00 01 00 02 00")
        bytes.append(contentsOf: [UInt8](repeating: 0, count: 512))
        guard let packet = ArtNetParser.parse(Data(bytes)) else {
            XCTFail("Пакет не распознан")
            return
        }
        XCTAssertEqual(packet.dmxData.count, 512, "Длина читается как big endian")
    }

    /// Ответ устройства на запрос обнаружения: первый байт 0x49.
    func testDeviceResponseParsing() throws {
        let fixture = try loadFixture()

        for vector in fixture.deviceResponse {
            let data = Data(bytes(from: vector.hex))
            let isDevice = ArtNetParser.isDeviceResponse(data)

            XCTAssertEqual(isDevice, vector.isDevice, "isDevice: \(vector.name)")

            if let expectedTag = vector.tag {
                XCTAssertEqual(
                    ArtNetParser.deviceTag(data),
                    expectedTag,
                    "tag: \(vector.name)")
            }
        }
    }

    /// Имя устройства лежит в байтах 47-58. Байты заполнены 'A'
    /// (0x41 в CP1251), чтобы проверить и смещение, и длину.
    func testDeviceTagOffset() {
        var bytes = bytes(from: "49 00 0D 11 00 00 00 00 00 00 00 00 00 00 00 00")
        // До смещения 47 уже есть 16 байт, добавляем до 47.
        bytes.append(contentsOf: [UInt8](repeating: 0x00, count: 31))
        XCTAssertEqual(bytes.count, 47)

        // Заполняем область имени 12 байтами 'A'.
        bytes.append(contentsOf: [UInt8](repeating: 0x41, count: 12))
        let data = Data(bytes)
        XCTAssertEqual(ArtNetParser.deviceTag(data), "AAAAAAAAAAAA", "12 байт со смещения 47")
    }

    /// Кириллица в имени устройства: именно для неё нужна Windows-1251.
    /// Байты CF F0 E8 E2 E5 F2 — это слово "Привет" в CP1251.
    func testDeviceTagCyrillic() {
        var bytes = bytes(from: "49 00 0D 11 00 00 00 00 00 00 00 00 00 00 00 00")
        bytes.append(contentsOf: [UInt8](repeating: 0x00, count: 31))
        bytes.append(contentsOf: [UInt8]([0xCF, 0xF0, 0xE8, 0xE2, 0xE5, 0xF2]))
        bytes.append(contentsOf: [UInt8](repeating: 0x00, count: 6))

        XCTAssertEqual(ArtNetParser.deviceTag(Data(bytes)), "Привет")
    }

    /// Слишком короткий ответ не должен ронять разбор.
    func testShortResponseIsSafe() {
        let data = Data(bytes(from: "49 00 0D"))
        XCTAssertFalse(ArtNetParser.isDeviceResponse(data))
        XCTAssertNil(ArtNetParser.deviceTag(data))
    }

    /// Мусор в сокете не должен приводить к падению.
    func testGarbageDoesNotCrash() {
        let data = Data([0x00, 0xFF, 0x13, 0x37, 0x42, 0x99])
        XCTAssertFalse(ArtNetParser.isDeviceResponse(data))
        XCTAssertNil(ArtNetParser.parse(data))
        XCTAssertNil(ArtNetParser.deviceTag(data))
    }

    /// Порог срабатывания Art-Net: 50 / 100 / 150.
    func testDefaultThresholds() {
        let thresholds = ArtNetThresholds.default
        XCTAssertEqual(thresholds.start, 50)
        XCTAssertEqual(thresholds.pause, 100)
        XCTAssertEqual(thresholds.stop, 150)
    }

    /// Реакция только на изменение значения: удержание кнопки на пульте
    /// не должно перезапускать трек многократно.
    func testActionTriggersOnlyOnChange() {
        var tracker = DmxCommandTracker()

        tracker.update(value: 50, last: 0)
        XCTAssertEqual(tracker.pending, .play)

        // То же значение повторно — никакого действия.
        tracker.update(value: 50, last: 50)
        XCTAssertNil(tracker.pending)

        // Новое значение — действие.
        tracker.update(value: 100, last: 50)
        XCTAssertEqual(tracker.pending, .pause)
    }

    /// Значение вне порогов не вызывает действие.
    func testValueOutsideThresholdsDoesNothing() {
        var tracker = DmxCommandTracker()

        tracker.update(value: 42, last: 0)
        XCTAssertNil(tracker.pending)

        tracker.update(value: 200, last: 42)
        XCTAssertNil(tracker.pending)
    }

    /// Приоритет: при совпадении нескольких порогов побеждает play.
    /// На практике пороги задаются разными значениями, но порядок
    /// проверки должен быть определён.
    func testThresholdPriority() {
        var tracker = DmxCommandTracker()
        var thresholds = ArtNetThresholds.default
        thresholds.start = 100

        tracker.update(value: 100, last: 0, thresholds: thresholds)
        XCTAssertEqual(tracker.pending, .play)
    }
}