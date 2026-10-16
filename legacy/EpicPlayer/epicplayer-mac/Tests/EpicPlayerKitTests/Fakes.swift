import EpicCore
import Foundation
@testable import EpicPlayerKit

struct FakeError: Error, CustomStringConvertible {
    let description: String
}

@MainActor
final class FakeAudioEngine: AudioEngine {
    var isLoaded = false
    var duration: TimeInterval = 0
    var currentTime: TimeInterval = 0
    var isPlaying = false
    var volume: Float = 0
    var onFinish: (() -> Void)?

    var failLoad = false
    var loadedURLs: [URL] = []
    var stopCount = 0

    func load(_ url: URL) throws {
        if failLoad { throw FakeError(description: "bad file") }
        loadedURLs.append(url)
        isLoaded = true
        duration = 300
        currentTime = 0
    }

    func play() { isPlaying = true }
    func pause() { isPlaying = false }

    func stop() {
        isPlaying = false
        currentTime = 0
        stopCount += 1
    }

    /// Трек доиграл.
    func finish() {
        isPlaying = false
        onFinish?()
    }
}

@MainActor
final class FakeTransport: ShowTransport {
    struct Sent: Equatable {
        let command: PlaybackCommand
        let position: UInt32
        let address: IPv4Address?
        let port: UInt16?
    }

    let interface: NetworkInterface
    var onDeviceResponse: ((DeviceResponse) -> Void)?

    var sent: [Sent] = []
    var deviceRequests = 0
    var announcements: [TCPAnnounceAddress] = []
    var isClosed = false
    var failSend = false

    init(interface: NetworkInterface) {
        self.interface = interface
    }

    func send(_ command: PlaybackCommand, positionMilliseconds: UInt32) throws {
        if failSend { throw FakeError(description: "sendto failed") }
        sent.append(Sent(command: command, position: positionMilliseconds, address: nil, port: nil))
    }

    func send(_ command: PlaybackCommand, positionMilliseconds: UInt32, to address: IPv4Address, port: UInt16) throws {
        if failSend { throw FakeError(description: "sendto failed") }
        sent.append(Sent(command: command, position: positionMilliseconds, address: address, port: port))
    }

    func requestDevices() throws {
        deviceRequests += 1
    }

    func announceTCPCallback(using mode: TCPAnnounceAddress) throws {
        announcements.append(mode)
    }

    func close() {
        isClosed = true
    }

    /// Последняя broadcast-команда.
    var lastBroadcast: Sent? { sent.last { $0.address == nil } }
}

@MainActor
final class FakeListener: CallbackListener {
    var onConnection: ((IPv4Address, UInt16) -> Void)?
    var isClosed = false

    func close() {
        isClosed = true
    }
}

@MainActor
final class FakeArtNet: ArtNetSource {
    let interface: NetworkInterface?
    var settings: DmxTriggerSettings
    var onCommand: ((PlaybackCommand) -> Void)?
    var isClosed = false

    init(interface: NetworkInterface?, settings: DmxTriggerSettings) {
        self.interface = interface
        self.settings = settings
    }

    func update(_ settings: DmxTriggerSettings) {
        self.settings = settings
    }

    func close() {
        isClosed = true
    }
}

func makeInterface(_ name: String, _ last: UInt8, kind: InterfaceKind = .wiredEthernet) -> NetworkInterface {
    NetworkInterface(
        name: name,
        index: UInt32(last),
        address: IPv4Address([192, 168, 88, last]),
        netmask: IPv4Address([255, 255, 255, 0]),
        kind: kind
    )
}

/// Модель с фейками. Все созданные фейки доступны для проверок.
@MainActor
final class Harness {
    let audio = FakeAudioEngine()
    let defaults: UserDefaults
    let store: SettingsStore
    var transports: [FakeTransport] = []
    var listeners: [FakeListener] = []
    var artNets: [FakeArtNet] = []
    var failTransport = false
    var failListener = false
    var failArtNet = false
    var interfaces = [makeInterface("en0", 10, kind: .wifi), makeInterface("en7", 20)]

    private(set) var model: PlayerModel!

    init(settings: AppSettings? = nil) {
        let suite = "EpicPlayerKitTests.\(UUID().uuidString)"
        defaults = UserDefaults(suiteName: suite)!
        defaults.removePersistentDomain(forName: suite)
        store = SettingsStore(defaults: defaults, debounce: 0)
        if let settings {
            store.save(settings)
        }

        model = PlayerModel(
            audio: audio,
            store: store,
            makeTransport: { [unowned self] interface in
                if self.failTransport { throw FakeError(description: "port busy") }
                let transport = FakeTransport(interface: interface)
                self.transports.append(transport)
                return transport
            },
            makeListener: { [unowned self] _ in
                if self.failListener { throw FakeError(description: "address in use") }
                let listener = FakeListener()
                self.listeners.append(listener)
                return listener
            },
            makeArtNet: { [unowned self] interface, settings in
                if self.failArtNet { throw FakeError(description: "6454 busy") }
                let source = FakeArtNet(interface: interface, settings: settings)
                self.artNets.append(source)
                return source
            },
            interfaceProvider: { [] }
        )
    }

    var transport: FakeTransport? { transports.last }

    /// Интерфейсы без фонового вызова провайдера.
    func connect() {
        model.applyInterfaces(interfaces)
    }

    /// Трек загружен, сеть подключена, отправленное очищено.
    func ready() {
        connect()
        model.openTrack(at: URL(fileURLWithPath: "/tmp/show.mp3"))
        transport?.sent.removeAll()
    }

    var savedSettings: AppSettings { store.load() }
}
