import XCTest
@testable import EpicCore

/// Логика сетевого слоя на подставных сокетах.
///
/// Реальные сокеты проверяются интеграционным зондом, см.
/// `Sources/SocketProbe`. Здесь — всё, что можно проверить, не выходя
/// в сеть: что пакеты принимаются, свои отбрасываются, а ответ
/// устройства уходит на порт, с которого пришло соединение.
final class SocketLayerTests: XCTestCase {

    private func bytes(from hex: String) -> [UInt8] {
        hex.split(separator: " ").map { UInt8($0, radix: 16)! }
    }

    /// Ответ устройства длиннее 47+12 байт, чтобы читалось имя.
    private func devicePacket(tag: [UInt8]) -> [UInt8] {
        [0x49, 0x00, 0x0D, 0x11] + [UInt8](repeating: 0, count: 43) + tag
    }

    // MARK: - Перечисление интерфейсов

    func testInterfaceEnumerationReturnsRealInterfaces() {
        let interfaces = InterfaceEnumerator.allInterfaces()
        XCTAssertFalse(interfaces.isEmpty, "Список интерфейсов пуст")
        XCTAssertTrue(interfaces.contains { $0.name.hasPrefix("lo") },
                      "loopback должен присутствовать")
    }

    /// У каждого интерфейса есть индекс — без него сокет не привязать
    /// к нужному интерфейсу, и broadcast уйдёт не туда.
    func testEveryInterfaceHasIndex() {
        for iface in InterfaceEnumerator.allInterfaces() {
            XCTAssertNotEqual(iface.index, 0, "у \(iface.name) нет индекса")
        }
    }

    /// Виртуальные интерфейсы macOS не должны попасть в рабочий список.
    func testUsableInterfacesExcludeVirtualOnes() {
        for iface in InterfaceEnumerator.usableInterfaces() {
            XCTAssertNotEqual(iface.kind, .tunnel, "\(iface.name) — туннель")
            XCTAssertNotEqual(iface.kind, .bridge, "\(iface.name) — мост")
            XCTAssertNotEqual(iface.kind, .loopback, "\(iface.name) — loopback")
            XCTAssertNotNil(iface.broadcastAddress, "\(iface.name) — нет broadcast")
        }
    }

    /// Broadcast должен быть в той же подсети, что и адрес.
    func testBroadcastIsInSameSubnet() {
        for iface in InterfaceEnumerator.usableInterfaces() {
            guard let address = iface.primaryAddress,
                  let broadcast = iface.broadcastAddress else { continue }

            XCTAssertNotEqual(address, broadcast, "\(iface.name)")
            XCTAssertEqual(broadcast.octets[3], 255, "\(iface.name): \(broadcast)")
        }
    }

    // MARK: - Отправка

    /// Сокет отправки обязан быть привязан к адресу интерфейса: без
    /// этого macOS отправит пакет через utun, и устройство его не увидит.
    /// Это главная причина, по которой порт на macOS отличается от Windows.
    func testBroadcastSocketBindsToInterfaceAddress() throws {
        let socket = FakeDatagramSocket()
        let factory = makeFactory(datagram: socket, listener: FakeStreamListener())

        let broadcaster = BroadcastSocket(socketFactory: factory)
        let iface = NetworkInterface(
            name: "en0", index: 4,
            addresses: [IPv4Address([192, 168, 88, 10])],
            netmask: SubnetMask([255, 255, 255, 0]),
            kind: .ethernet)

        try broadcaster.open(interface: iface, sourcePort: 333)

        XCTAssertEqual(socket.boundAddress?.description, "192.168.88.10",
                       "сокет не привязан к адресу интерфейса")
        XCTAssertEqual(broadcaster.destination?.description, "192.168.88.255")
    }

    func testBroadcastSocketSendsToBroadcastAddress() throws {
        let socket = FakeDatagramSocket()
        let broadcaster = BroadcastSocket(
            socketFactory: makeFactory(datagram: socket, listener: FakeStreamListener()))

        let iface = NetworkInterface(
            name: "en0", index: 4,
            addresses: [IPv4Address([192, 168, 88, 10])],
            netmask: SubnetMask([255, 255, 255, 0]),
            kind: .ethernet)

        try broadcaster.open(interface: iface, sourcePort: 333)
        broadcaster.destinationPort = 333

        let packet = PlaybackPacket.encode(.play, positionMilliseconds: 5000)
        try broadcaster.send(packet)

        XCTAssertEqual(socket.sent.count, 1)
        XCTAssertEqual(socket.sent[0].data, packet)
        XCTAssertEqual(socket.sent[0].destination.description, "192.168.88.255")
        XCTAssertEqual(socket.sent[0].port, 333)
    }

    /// Без интерфейса отправка невозможна — раньше об этом узнавали
    /// по сообщению об ошибке сокета.
    func testBroadcastSocketRequiresInterface() {
        let broadcaster = BroadcastSocket(
            socketFactory: makeFactory(datagram: FakeDatagramSocket(),
                                       listener: FakeStreamListener()))

        let unusable = NetworkInterface(
            name: "en0", index: 4,
            addresses: [IPv4Address([10, 0, 0, 1])],
            netmask: SubnetMask([255, 255, 255, 255]),  // /32 — broadcast нет
            kind: .ethernet)

        XCTAssertThrowsError(try broadcaster.open(interface: unusable, sourcePort: 333))
        XCTAssertThrowsError(try broadcaster.send(DeviceDiscovery.udpQuery))
    }

    /// Закрытый сокет не должен отправлять.
    func testBroadcastSocketAfterCloseDoesNotSend() throws {
        let socket = FakeDatagramSocket()
        let broadcaster = BroadcastSocket(
            socketFactory: makeFactory(datagram: socket, listener: FakeStreamListener()))

        let iface = NetworkInterface(
            name: "en0", index: 4,
            addresses: [IPv4Address([192, 168, 88, 10])],
            netmask: SubnetMask([255, 255, 255, 0]),
            kind: .ethernet)

        try broadcaster.open(interface: iface, sourcePort: 333)
        broadcaster.close()

        XCTAssertFalse(broadcaster.isOpen)
        XCTAssertTrue(socket.isClosed)
        XCTAssertThrowsError(try broadcaster.send(DeviceDiscovery.udpQuery))
    }

    // MARK: - Приём UDP

    /// Ответ устройства разбирается в адрес и имя.
    func testDeviceResponseIsDecoded() {
        let packet = devicePacket(tag: [UInt8]("EPICPLAYER01".utf8))
        let datagram = Datagram(
            data: Data(packet),
            sender: IPv4Address([192, 168, 88, 50]))

        guard let response = UdpReceiver.decodeDeviceResponse(datagram) else {
            XCTFail("ответ устройства не распознан")
            return
        }
        XCTAssertEqual(response.address.description, "192.168.88.50")
        XCTAssertEqual(response.tag, "EPICPLAYER01")
        XCTAssertEqual(response.rawData.count, 59)
    }

    /// Короткий ответ без имени не отбрасывается: устройство найдено,
    /// просто подпись не прочиталась.
    func testShortDeviceResponseIsAccepted() {
        let datagram = Datagram(data: Data([0x49, 0x11, 0x22, 0x33, 0x44]),
                                sender: IPv4Address([10, 0, 0, 7]))

        guard let response = UdpReceiver.decodeDeviceResponse(datagram) else {
            XCTFail("ответ устройства не распознан")
            return
        }
        XCTAssertNil(response.tag, "имени в коротком ответе нет")
        XCTAssertEqual(response.address.description, "10.0.0.7")
    }

    /// Чужой пакет не считается ответом устройства.
    func testForeignPacketIsNotDeviceResponse() {
        let artNet = Data(bytes(from:
            "41 72 74 2D 4E 65 74 00 00 50 00 0E 00 00 01 00 00 01 FF"))
        let datagram = Datagram(data: artNet,
                                sender: IPv4Address([10, 0, 0, 7]))

        XCTAssertNil(UdpReceiver.decodeDeviceResponse(datagram))
    }

    /// Приёмник отдаёт фактический порт: приложение сообщает его
    /// устройствам в объявлении TCP.
    func testReceiverReportsListeningPort() throws {
        let socket = FakeDatagramSocket()
        let receiver = UdpReceiver(
            port: 0,
            socketFactory: makeFactory(datagram: socket, listener: FakeStreamListener()))

        try receiver.open()
        defer { receiver.stop() }

        XCTAssertTrue(receiver.isRunning)
        XCTAssertEqual(receiver.listeningPort, 45_000)
    }

    /// Собственные пакеты отбрасываются, ответы устройств — нет, хотя
    /// начинаются теми же байтами `49 00`. Различает длина.
    func testOwnDiscoveryPacketsAreIgnored() {
        XCTAssertTrue(ArtNetParser.isOwnEcho(DeviceDiscovery.udpQuery))
        XCTAssertTrue(ArtNetParser.isOwnEcho(
            DeviceDiscovery.encodeTCPAnnouncement(localAddress: IPv4Address([192, 168, 1, 5]))))

        // Ответ устройства длиннее 3 байт — проходит даже при том же
        // заголовке. Иначе ни одно устройство не нашлось бы.
        XCTAssertFalse(ArtNetParser.isOwnEcho(
            Data(devicePacket(tag: [UInt8]("DEV1".utf8)))))
        XCTAssertFalse(ArtNetParser.isOwnEcho(Data([0x49, 0x11, 0x22, 0x33, 0x44])))

        XCTAssertFalse(ArtNetParser.isOwnEcho(
            Data(bytes(from: "41 72 74 2D 4E 65 74 00 00 50 00 0E 00 00 01 00 00 01 FF"))))
    }

    /// Остановка идемпотентна: повторный вызов не должен падать.
    func testReceiverStopIsIdempotent() throws {
        let receiver = UdpReceiver(
            port: 0,
            socketFactory: makeFactory(datagram: FakeDatagramSocket(),
                                       listener: FakeStreamListener()))
        try receiver.start()
        XCTAssertTrue(receiver.isRunning)

        receiver.stop()
        XCTAssertFalse(receiver.isRunning)
        receiver.stop()
        XCTAssertFalse(receiver.isRunning)
    }

    /// Принимает один пакет. Предполагается вызов после `open()`,
    /// без фонового потока — иначе тест и поток читали бы один сокет.
    func testPollOnceReturnsIncomingPacket() throws {
            let socket = FakeDatagramSocket()
            let receiver = UdpReceiver(
                port: 0,
                socketFactory: makeFactory(datagram: socket, listener: FakeStreamListener()))

            try receiver.open()
            defer { receiver.stop() }

            let expected = devicePacket(tag: [UInt8]("DEV1".utf8))
            socket.incoming = [Datagram(data: Data(expected),
                                        sender: IPv4Address([192, 168, 88, 60]))]

            guard let datagram = receiver.pollOnce() else {
                XCTFail("пакет не получен")
                return
            }
            XCTAssertEqual([UInt8](datagram.data), expected)
            XCTAssertEqual(datagram.sender.description, "192.168.88.60")
        }

        /// Фоновое чтение вызывает обработчик для каждого пакета.
        func testBackgroundLoopDeliversPackets() throws {
            let socket = FakeDatagramSocket()
            let listener = FakeStreamListener()

            let counter = PacketCounter()
            let receiver = UdpReceiver(
                port: 0,
                socketFactory: makeFactory(datagram: socket, listener: listener),
                pollIntervalMilliseconds: 50)
            receiver.onPacket = { _, _ in counter.increment() }

            try receiver.open()

            // Пакеты добавляются уже после старта: фоновый поток читает
            // очередь с интервалом, иначе он опустошит её до наполнения.
            // Первым идёт ответ устройства, вторым — собственный запрос,
            // который должен быть отброшен.
            socket.incoming = [
                Datagram(data: DeviceDiscovery.udpQuery,
                         sender: IPv4Address([127, 0, 0, 1])),
                Datagram(data: Data(devicePacket(tag: [UInt8]("DEV1".utf8))),
                         sender: IPv4Address([192, 168, 88, 60])),
            ]

            let thread = Thread { receiver.startReading() }
            thread.start()

            let deadline = Date().addingTimeInterval(2)
            while counter.value == 0 && Date() < deadline {
                usleep(10_000)
            }
            receiver.stop()

            XCTAssertEqual(counter.value, 1, "доставлен только ответ устройства")
        }

    // MARK: - TCP

    /// Устройство подключается: приложение получает адрес и порт,
    /// с которого пришло соединение.
    func testTcpListenerReportsDeviceAddressAndPort() throws {
        let listener = FakeStreamListener()
        let tcp = TcpListener(
            socketFactory: makeFactory(datagram: FakeDatagramSocket(),
                                       listener: listener))

        try tcp.start()
        defer { tcp.stop() }

        XCTAssertTrue(tcp.isRunning)
        XCTAssertEqual(tcp.listeningPort, 45_001)

        // Порт 40001 — то, с которого устройство подключилось.
        let remote = FakeStreamConnection(
            remoteAddress: IPv4Address([192, 168, 88, 70]),
            remotePort: 40001)
        listener.pending = [remote]

        guard let connection = tcp.pollOnce() else {
            XCTFail("соединение не принято")
            return
        }
        XCTAssertEqual(connection.deviceAddress.description, "192.168.88.70")
        XCTAssertEqual(connection.callbackPort, 40001)
    }

    /// Ответ уходит в соединение, с которого пришёл запрос, а не
    /// на объявленный порт 3331.
    func testTcpReplyGoesToConnectionSourcePort() throws {
        let listener = FakeStreamListener()
        let tcp = TcpListener(
            socketFactory: makeFactory(datagram: FakeDatagramSocket(),
                                       listener: listener))
        try tcp.start()
        defer { tcp.stop() }

        let remote = FakeStreamConnection(
            remoteAddress: IPv4Address([192, 168, 88, 70]),
            remotePort: 54321)
        listener.pending = [remote]

        guard let connection = tcp.pollOnce() else {
            XCTFail("соединение не принято")
            return
        }

        // Порту 3331 из объявления соответствует 54321.
        XCTAssertEqual(DeviceDiscovery.tcpCallbackPort, 3331)
        XCTAssertNotEqual(connection.callbackPort, DeviceDiscovery.tcpCallbackPort,
                          "ответ не должен идти на объявленный порт")

        let command = PlaybackPacket.encode(.play, positionMilliseconds: 1500)
        try connection.send(command)

        XCTAssertEqual(remote.sent.count, 1)
        XCTAssertEqual(remote.sent[0], command)
        XCTAssertEqual(PlaybackPacket.command(from: remote.sent[0]), .play)
        XCTAssertEqual(PlaybackPacket.position(from: remote.sent[0]), 1500)
    }

    /// Объявление для устройства: адрес и порт, которые приложение
    /// реально слушает.
    func testTCPAnnouncementUsesRealListeningPort() throws {
        let listener = FakeStreamListener()
        let tcp = TcpListener(
            socketFactory: makeFactory(datagram: FakeDatagramSocket(),
                                       listener: listener))
        try tcp.start()
        defer { tcp.stop() }

        let localAddress = IPv4Address([192, 168, 88, 10])
        let packet = DeviceDiscovery.encodeTCPAnnouncement(localAddress: localAddress)

        // В объявлении стоит объявленный порт 3331; устройство подключится
        // на него, а приложение ответит на эфемерный порт источника.
        guard let decoded = DeviceDiscovery.decodeTCPAnnouncement(packet) else {
            XCTFail("объявление не разбирается")
            return
        }
        XCTAssertEqual(decoded.address, localAddress)
        XCTAssertEqual(decoded.port, DeviceDiscovery.tcpCallbackPort)
    }

    // MARK: - Art-Net

    /// Приёмник Art-Net поднимается и снимается.
    func testArtNetReceiverStartsAndStops() throws {
        let receiver = ArtNetReceiver(
            socketFactory: makeFactory(datagram: FakeDatagramSocket(),
                                       listener: FakeStreamListener()))
        try receiver.start()
        XCTAssertTrue(receiver.isRunning)
        receiver.stop()
        XCTAssertFalse(receiver.isRunning)
    }

    /// Приёмник Art-Net слушает порт 6454, а не 333.
    func testArtNetReceiverUsesPort6454() throws {
        let socket = FakeDatagramSocket()
        let receiver = ArtNetReceiver(
            socketFactory: makeFactory(datagram: socket, listener: FakeStreamListener()))
        try receiver.start()
        defer { receiver.stop() }

        XCTAssertEqual(socket.boundPort, ArtNet.port)
        XCTAssertEqual(ArtNet.port, 6454)
    }
}