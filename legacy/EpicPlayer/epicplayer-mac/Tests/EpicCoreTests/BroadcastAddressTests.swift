import XCTest
@testable import EpicCore

/// Вычисление broadcast-адреса и фильтрация интерфейсов.
///
/// Windows-версия строит адрес заменой последнего октета на 255
/// (`MainWindow.xaml.cs:200`), то есть предполагает сеть /24. Для
/// 192.168.88.0/24 это верно, для 192.168.0.0/16 — нет.
final class BroadcastAddressTests: XCTestCase {

    /// /24 — случай, который покрыт логикой Windows-версии.
    func testSlash24() {
        let ip = IPv4Address([192, 168, 88, 253])
        let mask = SubnetMask([255, 255, 255, 0])
        XCTAssertEqual(ip.broadcastAddress(mask: mask)?.description, "192.168.88.255")
    }

    /// /16 — здесь Windows-версия посчитала бы 192.168.88.255,
    /// а правильный ответ 192.168.255.255.
    func testSlash16() {
        let ip = IPv4Address([192, 168, 88, 253])
        let mask = SubnetMask([255, 255, 0, 0])
        XCTAssertEqual(ip.broadcastAddress(mask: mask)?.description, "192.168.255.255")
    }

    /// /25 — сеть шире половины последнего октета.
    func testSlash25() {
        let ip = IPv4Address([192, 168, 88, 253])
        let mask = SubnetMask([255, 255, 255, 128])
        XCTAssertEqual(ip.broadcastAddress(mask: mask)?.description, "192.168.88.255")
    }

    /// /22 — 1024 адреса, broadcast заканчивается на 255.
    func testSlash22() {
        let ip = IPv4Address([192, 168, 88, 253])
        let mask = SubnetMask([255, 255, 252, 0])
        XCTAssertEqual(ip.broadcastAddress(mask: mask)?.description, "192.168.91.255")
    }

    /// /30 — сеть из четырёх адресов.
    func testSlash30() {
        let ip = IPv4Address([10, 0, 0, 6])
        let mask = SubnetMask([255, 255, 255, 252])
        XCTAssertEqual(ip.broadcastAddress(mask: mask)?.description, "10.0.0.7")
    }

    /// /32 — единственный адрес, широковещательного нет.
    /// Такие адреса бывают у point-to-point интерфейсов на macOS.
    func testSlash32() {
        let ip = IPv4Address([10, 0, 0, 1])
        let mask = SubnetMask([255, 255, 255, 255])
        XCTAssertNil(ip.broadcastAddress(mask: mask))
    }

    /// /31 — адреса точки到точки, широковещательного нет.
    func testSlash31() {
        let ip = IPv4Address([10, 0, 0, 0])
        let mask = SubnetMask([255, 255, 255, 254])
        XCTAssertNil(ip.broadcastAddress(mask: mask))
    }

    /// Разбор маски из строки, как это делает настройка Art-Net.
    func testMaskFromString() {
        XCTAssertEqual(SubnetMask("255.255.255.0")?.octets, [255, 255, 255, 0])
        XCTAssertEqual(SubnetMask("255.255.0.0")?.octets, [255, 255, 0, 0])
        XCTAssertNil(SubnetMask("255.255.255"))
        XCTAssertNil(SubnetMask("не маска"))
    }

    /// Индекс интерфейса, если известен, участвует в привязке сокета.
    /// Без него broadcast уходит по маршруту по умолчанию и может
    /// уйти в utun вместо выбранного интерфейса.
    func testInterfaceNeedsIndexForBinding() {
        let iface = NetworkInterface(
            name: "en0",
            index: 12,
            addresses: [IPv4Address([192, 168, 88, 253])],
            netmask: SubnetMask([255, 255, 255, 0]),
            kind: .ethernet)

        XCTAssertEqual(iface.index, 12)
        XCTAssertEqual(iface.broadcastAddress?.description, "192.168.88.255")
    }

    /// На macOS список интерфейсов содержит виртуальные: utun, awdl,
    /// Thunderbolt Bridge, bridge. Их нельзя предлагать для работы
    /// с устройством шоу.
    func testVirtualInterfacesAreFilteredOut() {
        let candidates = [
            NetworkInterface(name: "en0", index: 4,
                             addresses: [IPv4Address([192, 168, 88, 10])],
                             netmask: SubnetMask([255, 255, 255, 0]), kind: .ethernet),
            NetworkInterface(name: "en1", index: 5,
                             addresses: [IPv4Address([192, 168, 1, 20])],
                             netmask: SubnetMask([255, 255, 255, 0]), kind: .ethernet),
            NetworkInterface(name: "utun3", index: 15, addresses: [], netmask: nil, kind: .tunnel),
            NetworkInterface(name: "awdl0", index: 16, addresses: [], netmask: nil, kind: .other),
            NetworkInterface(name: "bridge0", index: 17, addresses: [], netmask: nil, kind: .bridge),
            NetworkInterface(name: "lo0", index: 1, addresses: [], netmask: nil, kind: .loopback),
        ]

        let usable = InterfaceFilter.usable(candidates)
        let names = usable.map { $0.name }

        XCTAssertEqual(names, ["en0", "en1"])
    }

    /// Интерфейс без адреса нельзя использовать: нечего привязывать.
    func testInterfaceWithoutAddressIsSkipped() {
        let candidates = [
            NetworkInterface(name: "en2", index: 8, addresses: [],
                             netmask: nil, kind: .ethernet),
        ]
        XCTAssertTrue(InterfaceFilter.usable(candidates).isEmpty)
    }

    /// Интерфейс без маски — адрес есть, но широковещательный
    /// вычислить нечем.
    func testInterfaceWithoutNetmaskIsSkipped() {
        let candidates = [
            NetworkInterface(name: "en3", index: 9,
                             addresses: [IPv4Address([10, 0, 0, 5])],
                             netmask: nil, kind: .ethernet),
        ]
        XCTAssertTrue(InterfaceFilter.usable(candidates).isEmpty)
    }

    /// Несколько адресов на одном интерфейсе: берётся первый.
    /// Выбор другого адреса не делается автоматически — если на macOS
    /// у интерфейса окажется не тот адрес, это обнаружится на устройстве.
    func testFirstAddressIsUsed() {
        let iface = NetworkInterface(
            name: "en0",
            index: 4,
            addresses: [IPv4Address([192, 168, 88, 10]), IPv4Address([10, 0, 0, 1])],
            netmask: SubnetMask([255, 255, 255, 0]),
            kind: .ethernet)

        XCTAssertEqual(iface.primaryAddress?.description, "192.168.88.10")
        XCTAssertEqual(iface.broadcastAddress?.description, "192.168.88.255")
    }

    /// Отображение для выпадающего списка интерфейсов.
    func testInterfaceDisplayName() {
        let iface = NetworkInterface(
            name: "en0",
            index: 4,
            addresses: [IPv4Address([192, 168, 88, 10])],
            netmask: SubnetMask([255, 255, 255, 0]),
            kind: .ethernet)

        XCTAssertEqual(iface.displayName, "en0 | 192.168.88.10")
    }
}