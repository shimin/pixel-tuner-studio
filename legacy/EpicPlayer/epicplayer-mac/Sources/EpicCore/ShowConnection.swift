import Foundation

/// Какой адрес класть в TCP-объявление (байты 2–5).
///
/// Расхождение 4 в plans/protocol.md: Windows-версия кладёт broadcast,
/// что похоже на ошибку. Оба варианта оставлены до проверки на устройстве.
public enum TCPAnnounceAddress {
    /// Адрес компьютера на выбранном интерфейсе.
    case interfaceAddress
    /// `x.x.x.255`, как в Windows-версии.
    case broadcastAddress
}

/// Канал протокола шоу на UDP 333 через выбранный интерфейс.
///
/// Как и в Windows-версии, один сокет и шлёт, и слушает 333: ответы
/// устройств приходят на тот же порт. Колбэки вызываются на очереди
/// сокета.
public final class ShowConnection: @unchecked Sendable {
    public static let port: UInt16 = 333

    public let interface: NetworkInterface

    /// Адрес назначения команд — по маске интерфейса, не только /24.
    public let broadcastAddress: IPv4Address

    private let socket: BroadcastSocket
    private let deviceHandler = Locked<((DeviceResponse) -> Void)?>(nil)
    private let otherHandler = Locked<((Datagram) -> Void)?>(nil)

    /// Ответы на запрос обнаружения.
    public var onDeviceResponse: ((DeviceResponse) -> Void)? {
        get { deviceHandler.get() }
        set { deviceHandler.set(newValue) }
    }

    /// Всё остальное, кроме собственных пакетов, — для разбора формата
    /// на устройстве.
    public var onOtherDatagram: ((Datagram) -> Void)? {
        get { otherHandler.get() }
        set { otherHandler.set(newValue) }
    }

    public init(interface: NetworkInterface) throws {
        self.interface = interface
        self.broadcastAddress = interface.broadcastAddress
        self.socket = try BroadcastSocket(port: Self.port, interface: interface)

        socket.onReceive = { [weak self] datagram in
            self?.handle(datagram)
        }
    }

    deinit {
        socket.close()
    }

    public func close() {
        socket.close()
    }

    /// Команда воспроизведения на broadcast.
    public func send(_ command: PlaybackCommand, positionMilliseconds: UInt32 = 0) throws {
        try socket.send(
            PlaybackPacket.encode(command, positionMilliseconds: positionMilliseconds),
            to: broadcastAddress,
            port: Self.port
        )
    }

    /// Команда одному устройству. Windows-версия делает так из окна
    /// device lookup: unicast на `WifiPort` (333), позиция всегда 0.
    public func send(_ command: PlaybackCommand, positionMilliseconds: UInt32 = 0, to device: IPv4Address) throws {
        try socket.send(
            PlaybackPacket.encode(command, positionMilliseconds: positionMilliseconds),
            to: device,
            port: Self.port
        )
    }

    /// UDP-запрос обнаружения `49 00 0D`. Ответы — в `onDeviceResponse`.
    public func requestDevices() throws {
        try socket.send(DeviceDiscovery.udpQuery, to: broadcastAddress, port: Self.port)
    }

    /// 9-байтовое объявление для TCP-обнаружения. Перед вызовом должен
    /// быть открыт `TCPCallbackListener`.
    public func announceTCPCallback(using mode: TCPAnnounceAddress = .interfaceAddress) throws {
        let address: IPv4Address
        switch mode {
        case .interfaceAddress: address = interface.address
        case .broadcastAddress: address = broadcastAddress
        }
        try socket.send(
            DeviceDiscovery.encodeTCPAnnouncement(localAddress: address),
            to: broadcastAddress,
            port: Self.port
        )
    }

    private func handle(_ datagram: Datagram) {
        // Свои broadcast-пакеты возвращаются на 333 — пропускаем.
        if datagram.sender == interface.address { return }

        if let response = DeviceResponse(data: datagram.data, sender: datagram.sender) {
            deviceHandler.get()?(response)
        } else {
            otherHandler.get()?(datagram)
        }
    }
}
