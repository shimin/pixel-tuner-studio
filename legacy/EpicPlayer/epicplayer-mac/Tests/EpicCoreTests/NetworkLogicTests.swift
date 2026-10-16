import Darwin
import XCTest
@testable import EpicCore

/// Логика сетевого слоя без сокетов: broadcast по маске, фильтр
/// интерфейсов, разбор ответа устройства, Art-Net и реакция на DMX.
final class NetworkLogicTests: XCTestCase {

    // MARK: - Broadcast

    func testBroadcastFor24MatchesWindows() {
        let address = IPv4Address([192, 168, 88, 253])
        let mask = IPv4Address([255, 255, 255, 0])
        XCTAssertEqual(address.broadcast(netmask: mask), IPv4Address([192, 168, 88, 255]))
        XCTAssertEqual(address.broadcast(netmask: mask), address.legacyBroadcast)
    }

    /// Расхождение 2 в protocol.md: Windows-версия дала бы 192.168.0.255.
    func testBroadcastFor16UsesMask() {
        let address = IPv4Address([192, 168, 0, 10])
        let mask = IPv4Address([255, 255, 0, 0])
        XCTAssertEqual(address.broadcast(netmask: mask), IPv4Address([192, 168, 255, 255]))
        XCTAssertNotEqual(address.broadcast(netmask: mask), address.legacyBroadcast)
    }

    func testBroadcastFor22() {
        let address = IPv4Address([10, 0, 5, 7])
        let mask = IPv4Address([255, 255, 252, 0])
        XCTAssertEqual(address.broadcast(netmask: mask), IPv4Address([10, 0, 7, 255]))
        XCTAssertEqual(mask.prefixLength, 22)
    }

    func testHostOrderValueRoundTrip() {
        let address = IPv4Address([192, 168, 88, 253])
        XCTAssertEqual(address.hostOrderValue, 0xC0A8_58FD)
        XCTAssertEqual(IPv4Address(hostOrderValue: 0xC0A8_58FD), address)
    }

    // MARK: - Фильтр интерфейсов

    private let upFlags = UInt32(IFF_UP | IFF_RUNNING | IFF_BROADCAST | IFF_MULTICAST)

    func testPhysicalInterfacesPass() {
        XCTAssertTrue(InterfaceEnumerator.isCandidate(name: "en0", flags: upFlags, kind: .wifi))
        XCTAssertTrue(InterfaceEnumerator.isCandidate(name: "en7", flags: upFlags, kind: .wiredEthernet))
        XCTAssertTrue(InterfaceEnumerator.isCandidate(name: "en5", flags: upFlags, kind: .unknown))
    }

    func testVirtualInterfacesRejected() {
        for name in ["lo0", "utun3", "awdl0", "llw0", "bridge0", "bridge100", "ap1", "anpi0", "vmenet0"] {
            XCTAssertFalse(InterfaceEnumerator.isCandidate(name: name, flags: upFlags, kind: .unknown), name)
        }
    }

    /// Префикс засчитывается только перед цифрой: `apple0` не точка доступа.
    func testPrefixRequiresDigit() {
        XCTAssertTrue(InterfaceEnumerator.isCandidate(name: "apple0", flags: upFlags, kind: .unknown))
    }

    func testDownOrPointToPointRejected() {
        XCTAssertFalse(InterfaceEnumerator.isCandidate(name: "en0", flags: UInt32(IFF_BROADCAST), kind: .wifi))
        XCTAssertFalse(InterfaceEnumerator.isCandidate(name: "en0", flags: upFlags | UInt32(IFF_POINTOPOINT), kind: .wifi))
    }

    func testOtherKindRejected() {
        XCTAssertFalse(InterfaceEnumerator.isCandidate(name: "en9", flags: upFlags, kind: .other))
    }

    // MARK: - Ответ устройства

    private let device = IPv4Address([192, 168, 88, 50])

    private func response(name: [UInt8], length: Int = 64) -> Data {
        var bytes = [UInt8](repeating: 0xAA, count: length)
        bytes[0] = 0x49
        for i in DeviceResponse.nameRange { bytes[i] = 0 }
        for (offset, byte) in name.enumerated() { bytes[DeviceResponse.nameRange.lowerBound + offset] = byte }
        return Data(bytes)
    }

    /// «Шоу 1» в Windows-1251: D8 EE F3 20 31.
    func testDeviceNameIsCP1251AndTrimmed() {
        let data = response(name: [0xD8, 0xEE, 0xF3, 0x20, 0x31])
        let parsed = DeviceResponse(data: data, sender: device)
        XCTAssertEqual(parsed?.name, "Шоу 1")
        XCTAssertEqual(parsed?.address, device)
    }

    /// Собственный запрос, вернувшийся через broadcast, — не ответ.
    func testOwnQueryIsNotResponse() {
        XCTAssertNil(DeviceResponse(data: DeviceDiscovery.udpQuery, sender: device))
    }

    func testPlaybackPacketIsNotResponse() {
        XCTAssertNil(DeviceResponse(data: PlaybackPacket.encode(.play), sender: device))
    }

    /// Короткий ответ: Windows бросила бы исключение, здесь имени просто нет.
    func testShortResponseHasNoName() {
        let parsed = DeviceResponse(data: Data([0x49, 0x01, 0x02, 0x0D]), sender: device)
        XCTAssertNotNil(parsed)
        XCTAssertNil(parsed?.name)
        XCTAssertEqual(parsed?.rawHex, "49 01 02 0D")
    }

    // MARK: - ArtDmx

    func testArtDmxRoundTrip() {
        let packet = ArtDmxPacket(sequence: 7, physical: 0, universe: 0x0102, data: [50, 0, 255, 1])
        XCTAssertEqual(ArtDmxPacket.parse(packet.encoded()), packet)
    }

    /// Байты по спецификации: OpCode little endian, длина big endian.
    func testArtDmxWireFormat() {
        let packet = ArtDmxPacket(sequence: 1, physical: 2, universe: 0x0103, data: [10, 20])
        XCTAssertEqual(
            packet.encoded().hexString,
            "41 72 74 2D 4E 65 74 00 00 50 00 0E 01 02 03 01 00 02 0A 14"
        )
    }

    func testArtDmxRejectsOtherOpCodes() {
        var bytes = [UInt8](ArtDmxPacket(sequence: 0, physical: 0, universe: 0, data: [1, 2]).encoded())
        bytes[9] = 0x20   // ArtPoll
        XCTAssertNil(ArtDmxPacket.parse(Data(bytes)))
    }

    func testArtDmxRejectsTruncated() {
        let full = ArtDmxPacket(sequence: 0, physical: 0, universe: 0, data: [1, 2, 3, 4]).encoded()
        XCTAssertNil(ArtDmxPacket.parse(full.dropLast()))
        XCTAssertNil(ArtDmxPacket.parse(Data("Art-Net".utf8)))
    }

    // MARK: - Реакция на DMX

    private func dmx(_ value: UInt8, channel: Int = 0, universe: UInt16 = 0) -> ArtDmxPacket {
        var data = [UInt8](repeating: 0, count: 512)
        data[channel] = value
        return ArtDmxPacket(sequence: 0, physical: 0, universe: universe, data: data)
    }

    func testTriggerFiresOnChangeOnly() {
        var trigger = DmxTrigger()
        XCTAssertEqual(trigger.process(dmx(50)), .play)
        XCTAssertNil(trigger.process(dmx(50)), "Удержание не перезапускает трек")
        XCTAssertEqual(trigger.process(dmx(100)), .pause)
        XCTAssertEqual(trigger.process(dmx(150)), .stop)
        XCTAssertNil(trigger.process(dmx(0)))
        XCTAssertEqual(trigger.process(dmx(50)), .play)
    }

    /// Фейдер проходит через значения — срабатывает только точное.
    func testTriggerIgnoresIntermediateValues() {
        var trigger = DmxTrigger()
        XCTAssertNil(trigger.process(dmx(49)))
        XCTAssertEqual(trigger.process(dmx(50)), .play)
        XCTAssertNil(trigger.process(dmx(51)))
    }

    func testTriggerChannelAndUniverse() {
        var trigger = DmxTrigger(settings: DmxTriggerSettings(channel: 9, universe: 1))
        XCTAssertNil(trigger.process(dmx(50, channel: 9, universe: 0)), "Чужой universe")
        XCTAssertNil(trigger.process(dmx(50, channel: 0, universe: 1)), "Чужой канал")
        XCTAssertEqual(trigger.process(dmx(50, channel: 9, universe: 1)), .play)
    }

    func testTriggerChannelOutOfRange() {
        var trigger = DmxTrigger(settings: DmxTriggerSettings(channel: 600))
        XCTAssertNil(trigger.process(dmx(50)))
    }
}
