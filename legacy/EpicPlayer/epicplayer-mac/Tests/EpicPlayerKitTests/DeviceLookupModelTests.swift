import EpicCore
import XCTest
@testable import EpicPlayerKit

@MainActor
final class DeviceLookupModelTests: XCTestCase {

    private func response(from last: UInt8, name: [UInt8] = [0x41], extra: UInt8 = 0) -> DeviceResponse {
        var bytes = [UInt8](repeating: 0, count: 64)
        bytes[0] = 0x49
        bytes[1] = extra
        for (offset, byte) in name.enumerated() { bytes[47 + offset] = byte }
        return DeviceResponse(data: Data(bytes), sender: IPv4Address([192, 168, 88, last]))!
    }

    func testUdpLookupSendsQueryOnOpen() {
        let h = Harness()
        h.connect()

        let lookup = h.model.openLookup(mode: .udp)

        XCTAssertNotNil(lookup)
        XCTAssertEqual(h.transport?.deviceRequests, 1)
        XCTAssertTrue(h.listeners.isEmpty, "UDP-режим без TCP-сервера")
    }

    func testLookupWithoutInterfaceFails() {
        let h = Harness()
        XCTAssertNil(h.model.openLookup(mode: .udp))
        XCTAssertNotNil(h.model.networkError)
    }

    func testResponsesListedAndDeduplicated() {
        let h = Harness()
        h.connect()
        let lookup = h.model.openLookup(mode: .udp)!

        h.transport?.onDeviceResponse?(response(from: 50, name: [0x41]))
        h.transport?.onDeviceResponse?(response(from: 51, name: [0x42]))
        h.transport?.onDeviceResponse?(response(from: 50, name: [0x41], extra: 7))

        XCTAssertEqual(lookup.devices.map(\.address.description), ["192.168.88.50", "192.168.88.51"])
        XCTAssertEqual(lookup.devices.first?.name, "A")
        XCTAssertTrue(lookup.devices.first?.rawHex?.hasPrefix("49 07") ?? false, "Повтор обновляет сырые байты")
        XCTAssertEqual(lookup.totalTitle, "Total: 2")
    }

    func testResponsesIgnoredAfterClose() {
        let h = Harness()
        h.connect()
        let lookup = h.model.openLookup(mode: .udp)!
        h.model.closeLookup()

        h.transport?.onDeviceResponse?(response(from: 50))

        XCTAssertTrue(lookup.devices.isEmpty)
    }

    func testRefreshRepeatsQuery() {
        let h = Harness()
        h.connect()
        let lookup = h.model.openLookup(mode: .udp)!

        lookup.refresh()

        XCTAssertEqual(h.transport?.deviceRequests, 2)
    }

    /// `WifiInfoDevice`: unicast на 333, позиция 0.
    func testRowCommandsAreUnicastTo333() {
        let h = Harness()
        h.connect()
        let lookup = h.model.openLookup(mode: .udp)!
        h.transport?.onDeviceResponse?(response(from: 50))
        h.transport?.sent.removeAll()

        lookup.play(lookup.devices[0])
        lookup.stop(lookup.devices[0])

        let device = IPv4Address([192, 168, 88, 50])
        XCTAssertEqual(h.transport?.sent, [
            FakeTransport.Sent(command: .play, position: 0, address: device, port: 333),
            FakeTransport.Sent(command: .stop, position: 0, address: device, port: 333),
        ])
    }

    func testTcpLookupListensQueriesAndAnnounces() {
        let h = Harness()
        h.connect()

        h.model.openLookup(mode: .tcp)

        XCTAssertEqual(h.listeners.count, 1)
        XCTAssertEqual(h.transport?.deviceRequests, 1)
        XCTAssertEqual(h.transport?.announcements, [TCPAnnounceAddress.interfaceAddress])
    }

    func testTcpConnectionsListedWithTheirPort() {
        let h = Harness()
        h.connect()
        let lookup = h.model.openLookup(mode: .tcp)!
        let device = IPv4Address([192, 168, 88, 60])

        h.listeners.last?.onConnection?(device, 50123)
        h.listeners.last?.onConnection?(device, 50124)
        h.transport?.onDeviceResponse?(response(from: 61))

        XCTAssertEqual(lookup.devices.count, 1, "Повторное подключение и UDP-ответы не добавляют строк")
        XCTAssertEqual(lookup.devices.first?.port, 50124)

        h.transport?.sent.removeAll()
        lookup.play(lookup.devices[0])
        XCTAssertEqual(h.transport?.sent.first?.port, 50124)
    }

    func testAnnounceAddressSwitchReannounces() {
        let h = Harness()
        h.connect()
        let lookup = h.model.openLookup(mode: .tcp)!

        lookup.setAnnounceAddress(.broadcastAddress)

        XCTAssertEqual(h.transport?.announcements, [TCPAnnounceAddress.interfaceAddress, .broadcastAddress])
    }

    func testTcpListenerFailureReported() {
        let h = Harness()
        h.connect()
        h.failListener = true

        let lookup = h.model.openLookup(mode: .tcp)!

        XCTAssertNotNil(lookup.error)
        XCTAssertTrue(h.transport?.announcements.isEmpty ?? false)
    }

    func testClosingLookupClosesListener() {
        let h = Harness()
        h.connect()
        h.model.openLookup(mode: .tcp)

        h.model.closeLookup()

        XCTAssertEqual(h.listeners.last?.isClosed, true)
        XCTAssertNil(h.model.lookup)
    }
}
