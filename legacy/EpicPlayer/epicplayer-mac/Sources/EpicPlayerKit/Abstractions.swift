import EpicCore
import Foundation

// Границы между моделями и внешним миром. В приложении — адаптеры над
// EpicCore и AVFoundation (Live*.swift, SystemAudioPlayer.swift),
// в тестах — фейки. См. plans/app-design.md, раздел 3.
//
// Все колбэки протоколов вызываются на главном потоке: адаптеры
// перекладывают их с очередей сокетов сами.

/// Воспроизведение файла. См. plans/app-design.md, раздел 4.
@MainActor
public protocol AudioEngine: AnyObject {
    var isLoaded: Bool { get }
    var duration: TimeInterval { get }
    var currentTime: TimeInterval { get }
    var isPlaying: Bool { get }
    /// 0…1.
    var volume: Float { get set }
    /// Трек доиграл до конца.
    var onFinish: (() -> Void)? { get set }

    func load(_ url: URL) throws
    func play()
    func pause()
    /// Останавливает и сбрасывает позицию в 0, как `MediaPlayer.Stop()`.
    func stop()
}

/// Канал протокола шоу через выбранный интерфейс — `ShowConnection`.
@MainActor
public protocol ShowTransport: AnyObject {
    var interface: NetworkInterface { get }
    var onDeviceResponse: ((DeviceResponse) -> Void)? { get set }

    /// На broadcast выбранного интерфейса.
    func send(_ command: PlaybackCommand, positionMilliseconds: UInt32) throws
    /// Unicast одному устройству.
    func send(_ command: PlaybackCommand, positionMilliseconds: UInt32, to address: IPv4Address, port: UInt16) throws
    func requestDevices() throws
    func announceTCPCallback(using mode: TCPAnnounceAddress) throws
    func close()
}

/// TCP-сервер для TCP device lookup — `TCPCallbackListener`.
@MainActor
public protocol CallbackListener: AnyObject {
    /// Устройство подключилось: адрес и порт, с которого пришло соединение.
    var onConnection: ((IPv4Address, UInt16) -> Void)? { get set }
    /// Закрывает сервер и все принятые соединения.
    func close()
}

/// Приём Art-Net с уже применённым `DmxTrigger`: наружу выходят только
/// сработавшие команды, а не 40 пакетов в секунду.
@MainActor
public protocol ArtNetSource: AnyObject {
    var onCommand: ((PlaybackCommand) -> Void)? { get set }
    func update(_ settings: DmxTriggerSettings)
    func close()
}

public typealias ShowTransportFactory = @MainActor (NetworkInterface) throws -> ShowTransport
public typealias CallbackListenerFactory = @MainActor (NetworkInterface) throws -> CallbackListener
/// `nil` вместо интерфейса — принимать с любого.
public typealias ArtNetSourceFactory = @MainActor (NetworkInterface?, DmxTriggerSettings) throws -> ArtNetSource
/// Блокирующий вызов, выполняется не на главном потоке.
public typealias InterfaceProvider = @Sendable () -> [NetworkInterface]

/// Выполнить на главном потоке в порядке поступления. `Task { @MainActor }`
/// порядок не гарантирует, а для пакетов он важен.
func onMain(_ body: @escaping @MainActor () -> Void) {
    DispatchQueue.main.async {
        MainActor.assumeIsolated(body)
    }
}
