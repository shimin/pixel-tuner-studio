import EpicCore
import Foundation

// Адаптеры EpicCore к протоколам моделей. Сокеты зовут колбэки на своих
// очередях; здесь они переходят на главный поток (plans/app-design.md, 3).
//
// Колбэки сокетов помечены @Sendable: иначе замыкание, созданное в
// @MainActor-инициализаторе, унаследует изоляцию главного актора, хотя
// вызывается с очереди сокета.

@MainActor
final class LiveShowTransport: ShowTransport {
    private let connection: ShowConnection

    var interface: NetworkInterface { connection.interface }
    var onDeviceResponse: ((DeviceResponse) -> Void)?

    init(interface: NetworkInterface) throws {
        connection = try ShowConnection(interface: interface)
        connection.onDeviceResponse = { @Sendable [weak self] response in
            onMain { self?.onDeviceResponse?(response) }
        }
    }

    func send(_ command: PlaybackCommand, positionMilliseconds: UInt32) throws {
        try connection.send(command, positionMilliseconds: positionMilliseconds)
    }

    func send(_ command: PlaybackCommand, positionMilliseconds: UInt32, to address: IPv4Address, port: UInt16) throws {
        try connection.send(command, positionMilliseconds: positionMilliseconds, to: address, port: port)
    }

    func requestDevices() throws {
        try connection.requestDevices()
    }

    func announceTCPCallback(using mode: TCPAnnounceAddress) throws {
        try connection.announceTCPCallback(using: mode)
    }

    func close() {
        connection.close()
    }
}

@MainActor
final class LiveCallbackListener: CallbackListener {
    private let listener: TCPCallbackListener
    /// Соединения держатся здесь: без ссылки они закрываются сразу.
    private var connections: [TCPDeviceConnection] = []

    var onConnection: ((IPv4Address, UInt16) -> Void)?

    init(interface: NetworkInterface) throws {
        listener = try TCPCallbackListener(interface: interface)
        listener.onConnection = { @Sendable [weak self] connection in
            let address = connection.remoteAddress
            let port = connection.remotePort
            onMain {
                guard let self else {
                    connection.close()
                    return
                }
                self.connections.append(connection)
                self.onConnection?(address, port)
            }
        }
    }

    func close() {
        listener.close()
        connections.forEach { $0.close() }
        connections.removeAll()
    }
}

/// `DmxTrigger` под замком: пакеты идут с очереди сокета, настройки
/// меняются с главного потока.
final class LockedTrigger: @unchecked Sendable {
    private var trigger: DmxTrigger
    private let lock = NSLock()

    init(_ settings: DmxTriggerSettings) {
        trigger = DmxTrigger(settings: settings)
    }

    func process(_ packet: ArtDmxPacket) -> PlaybackCommand? {
        lock.lock()
        defer { lock.unlock() }
        return trigger.process(packet)
    }

    func update(_ settings: DmxTriggerSettings) {
        lock.lock()
        defer { lock.unlock() }
        trigger.settings = settings
    }
}

@MainActor
final class LiveArtNetSource: ArtNetSource {
    private let receiver: ArtNetReceiver
    private let trigger: LockedTrigger

    var onCommand: ((PlaybackCommand) -> Void)?

    init(interface: NetworkInterface?, settings: DmxTriggerSettings) throws {
        let trigger = LockedTrigger(settings)
        self.trigger = trigger
        receiver = try ArtNetReceiver(interface: interface)
        receiver.onDmx = { @Sendable [weak self] packet, _ in
            // Триггер — на очереди сокета, на главный поток уходит только
            // сработавшая команда.
            guard let command = trigger.process(packet) else { return }
            onMain { self?.onCommand?(command) }
        }
    }

    func update(_ settings: DmxTriggerSettings) {
        trigger.update(settings)
    }

    func close() {
        receiver.close()
    }
}

extension PlayerModel {
    /// Модель с настоящими сокетами, звуком и `UserDefaults`.
    public static func live() -> PlayerModel {
        PlayerModel(
            audio: SystemAudioPlayer(),
            store: SettingsStore(),
            makeTransport: { try LiveShowTransport(interface: $0) },
            makeListener: { try LiveCallbackListener(interface: $0) },
            makeArtNet: { try LiveArtNetSource(interface: $0, settings: $1) },
            interfaceProvider: { InterfaceEnumerator.available() }
        )
    }
}
