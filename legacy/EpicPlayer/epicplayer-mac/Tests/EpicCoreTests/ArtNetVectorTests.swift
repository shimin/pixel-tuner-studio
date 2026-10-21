import EpicCore
import XCTest

/// Эталоны Art-Net и ответа устройства из Tests/Fixtures/artnet-vectors.json.
///
/// Байты выведены из `eDmx.ArtNet` (`ArtNetPacket.cs`, `ArtNetDmxPacket.cs`)
/// независимо от `ArtDmx.swift`, поэтому сверка ловит ошибки порядка байт:
/// OpCode — little endian, версия и длина — big endian, Universe — младший
/// байт первым.
final class ArtNetVectorTests: XCTestCase {
    private struct Fixture: Decodable {
        let artDmx: [ArtDmxVector]
        let channelThresholds: [Thresholds]
        let deviceResponse: [ResponseVector]
    }

    private struct ArtDmxVector: Decodable {
        let name: String
        let universe: UInt16
        let sequence: UInt8
        let physical: UInt8
        let dmxValues: [UInt8]?
        let dmxCount: Int?
        let expectedHex: String?
        let expectedPrefix: String?
    }

    private struct Thresholds: Decodable {
        let start: UInt8
        let pause: UInt8
        let stop: UInt8
    }

    private struct ResponseVector: Decodable {
        let name: String
        let hex: String
        let isDevice: Bool
        let tag: String?
    }

    private static var fixtureURL: URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()   // EpicCoreTests
            .deletingLastPathComponent()   // Tests
            .appendingPathComponent("Fixtures/artnet-vectors.json")
    }

    private func loadFixture() throws -> Fixture {
        let data = try Data(contentsOf: Self.fixtureURL)
        return try JSONDecoder().decode(Fixture.self, from: data)
    }

    private func bytes(from hex: String) -> [UInt8] {
        hex.split(separator: " ").compactMap { UInt8($0, radix: 16) }
    }

    private let sender = IPv4Address([192, 168, 88, 20])

    // MARK: - ArtDmx

    func testArtDmxVectorsParse() throws {
        let fixture = try loadFixture()
        XCTAssertFalse(fixture.artDmx.isEmpty, "Эталоны не загрузились")

        for vector in fixture.artDmx {
            let data: Data
            let expectedValues: [UInt8]
            if let hex = vector.expectedHex, let values = vector.dmxValues {
                data = Data(bytes(from: hex))
                expectedValues = values
            } else if let prefix = vector.expectedPrefix, let count = vector.dmxCount {
                expectedValues = [UInt8](repeating: 0, count: count)
                data = Data(bytes(from: prefix) + expectedValues)
            } else {
                XCTFail("В эталоне нет байтов: \(vector.name)")
                continue
            }

            guard let packet = ArtDmxPacket.parse(data) else {
                XCTFail("Пакет не распознан: \(vector.name)")
                continue
            }
            XCTAssertEqual(packet.universe, vector.universe, vector.name)
            XCTAssertEqual(packet.sequence, vector.sequence, vector.name)
            XCTAssertEqual(packet.physical, vector.physical, vector.name)
            XCTAssertEqual(packet.data, expectedValues, vector.name)
        }
    }

    func testArtDmxVectorsEncode() throws {
        let fixture = try loadFixture()

        for vector in fixture.artDmx {
            let packet = ArtDmxPacket(
                sequence: vector.sequence,
                physical: vector.physical,
                universe: vector.universe,
                data: vector.dmxValues ?? [UInt8](repeating: 0, count: vector.dmxCount ?? 0)
            )
            let encoded = [UInt8](packet.encoded())

            if let hex = vector.expectedHex {
                XCTAssertEqual(encoded, bytes(from: hex), vector.name)
            } else if let prefix = vector.expectedPrefix {
                let expected = bytes(from: prefix)
                XCTAssertEqual(Array(encoded.prefix(expected.count)), expected, vector.name)
            }
        }
    }

    func testDefaultThresholdsMatchWindows() throws {
        let fixture = try loadFixture()
        let defaults = DmxTriggerSettings()

        for thresholds in fixture.channelThresholds {
            XCTAssertEqual(defaults.playValue, thresholds.start)
            XCTAssertEqual(defaults.pauseValue, thresholds.pause)
            XCTAssertEqual(defaults.stopValue, thresholds.stop)
        }
    }

    // MARK: - Ответ устройства

    func testDeviceResponseVectors() throws {
        let fixture = try loadFixture()
        XCTAssertFalse(fixture.deviceResponse.isEmpty, "Эталоны не загрузились")

        for vector in fixture.deviceResponse {
            let response = DeviceResponse(data: Data(bytes(from: vector.hex)), sender: sender)
            XCTAssertEqual(response != nil, vector.isDevice, vector.name)
            XCTAssertEqual(response?.name, vector.tag, vector.name)
        }
    }

    func testCyrillicTagUsesWindows1251() {
        // «Привет» в CP1251. Под номером 1251 в CFStringEncoding лежит
        // не CP1251, поэтому важно, что используется .windowsCP1251.
        var bytes = [UInt8](repeating: 0, count: 59)
        bytes[0] = 0x49
        let name: [UInt8] = [0xCF, 0xF0, 0xE8, 0xE2, 0xE5, 0xF2]
        bytes.replaceSubrange(47..<(47 + name.count), with: name)

        XCTAssertEqual(DeviceResponse(data: Data(bytes), sender: sender)?.name, "Привет")
    }
}
