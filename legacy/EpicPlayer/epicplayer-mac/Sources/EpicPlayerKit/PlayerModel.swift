import EpicCore
import Foundation
import Observation

/// Состояние и команды главного окна. Поведение перенесено из
/// `MainWindow.xaml.cs`, отступления — plans/app-design.md, раздел 9.
@MainActor
@Observable
public final class PlayerModel {
    public enum Status: Equatable, Sendable {
        case stopped, playing, paused

        /// Подписи как `[Description]` у `Status` в Windows-версии.
        public var title: String {
            switch self {
            case .stopped: return "Status: Stop"
            case .playing: return "Status: Play"
            case .paused: return "Status: Pause"
            }
        }
    }

    // MARK: - Состояние для UI

    public private(set) var status: Status = .stopped
    public private(set) var hasTrack = false
    public private(set) var trackTitle: String?
    /// «File not found», ошибка декодирования и т. п.
    public private(set) var trackError: String?
    public private(set) var position: TimeInterval = 0
    public private(set) var duration: TimeInterval = 0
    public private(set) var volume: Double = 0.5

    public private(set) var interfaces: [NetworkInterface] = []
    public private(set) var selectedInterface: NetworkInterface?
    /// Последняя ошибка сети; сбрасывается успешной отправкой.
    public private(set) var networkError: String?

    public private(set) var artNet = ArtNetSettings()
    public private(set) var isArtNetRunning = false
    public private(set) var artNetError: String?

    /// Открытое окно device lookup. Пока оно открыто, периодическая
    /// отправка выключена — как `_suppressPlaybackCommands` в Windows.
    public private(set) var lookup: DeviceLookupModel?

    public var progress: Double {
        duration > 0 ? min(max(position / duration, 0), 1) : 0
    }

    // MARK: - Зависимости

    private let audio: AudioEngine
    private let store: SettingsStore
    private let makeTransport: ShowTransportFactory
    private let makeListener: CallbackListenerFactory
    private let makeArtNet: ArtNetSourceFactory
    private let interfaceProvider: InterfaceProvider

    @ObservationIgnored private var settings: AppSettings
    @ObservationIgnored private var transport: ShowTransport?
    @ObservationIgnored private var artNetSource: ArtNetSource?
    /// Интерфейс, на котором открыт `artNetSource` (`nil` — любой).
    @ObservationIgnored private var artNetInterfaceName: String?

    public init(
        audio: AudioEngine,
        store: SettingsStore,
        makeTransport: @escaping ShowTransportFactory,
        makeListener: @escaping CallbackListenerFactory,
        makeArtNet: @escaping ArtNetSourceFactory,
        interfaceProvider: @escaping InterfaceProvider
    ) {
        self.audio = audio
        self.store = store
        self.makeTransport = makeTransport
        self.makeListener = makeListener
        self.makeArtNet = makeArtNet
        self.interfaceProvider = interfaceProvider
        self.settings = store.load()

        volume = settings.volume
        artNet = settings.artNet
        audio.volume = Float(volume)
        audio.onFinish = { [weak self] in self?.trackDidFinish() }
    }

    /// Запуск: трек из настроек, список интерфейсов, Art-Net.
    public func start() async {
        restoreTrack()
        await reloadInterfaces()
        if artNet.enabled {
            startArtNet()
        }
    }

    /// Закрыть сокеты и записать настройки — при выходе.
    public func shutdown() {
        closeLookup()
        artNetSource?.close()
        artNetSource = nil
        transport?.close()
        transport = nil
        store.flush()
    }

    // MARK: - Трек

    public func openTrack(at url: URL) {
        load(url)
        if hasTrack {
            settings.audioFilePath = url.path
            store.save(settings)
        }
    }

    private func restoreTrack() {
        guard let path = settings.audioFilePath else { return }
        let url = URL(fileURLWithPath: path)
        guard FileManager.default.fileExists(atPath: path) else {
            // Путь остаётся в настройках: диск могут подключить позже.
            trackTitle = url.lastPathComponent
            trackError = "File not found"
            hasTrack = false
            return
        }
        load(url)
    }

    /// Новый трек всегда в состоянии stop. Windows-версия ставила статус
    /// «Play», не начиная воспроизведение, — не переносится.
    private func load(_ url: URL) {
        if audio.isLoaded {
            audio.stop()
        }
        status = .stopped
        position = 0
        trackTitle = url.lastPathComponent

        do {
            try audio.load(url)
            hasTrack = true
            trackError = nil
            duration = audio.duration
        } catch {
            hasTrack = false
            duration = 0
            trackError = "Cannot open: \(error.localizedDescription)"
        }
    }

    // MARK: - Транспорт

    public func play() {
        guard hasTrack else { return }
        audio.play()
        status = .playing
        refresh()
        send(.play, positionMilliseconds: currentMilliseconds)
    }

    /// В состоянии stop игнорируется, как в Windows-версии.
    public func pause() {
        guard hasTrack, status != .stopped else { return }
        audio.pause()
        status = .paused
        refresh()
        send(.pause, positionMilliseconds: currentMilliseconds)
    }

    /// Позиция в пакете — после сброса, то есть 0: Windows-версия читает
    /// `player.Position` после `player.Stop()`.
    public func stop() {
        guard hasTrack else { return }
        audio.stop()
        status = .stopped
        refresh()
        send(.stop, positionMilliseconds: currentMilliseconds)
    }

    /// Только в сеть: локальный трек не меняется.
    public func previousTrack() {
        guard hasTrack else { return }
        send(.previousTrack, positionMilliseconds: 0)
    }

    public func nextTrack() {
        guard hasTrack else { return }
        send(.nextTrack, positionMilliseconds: 0)
    }

    public func setVolume(_ value: Double) {
        let clamped = min(max(value, 0), 1)
        volume = clamped
        audio.volume = Float(clamped)
        settings.volume = clamped
        store.save(settings)
    }

    /// Конец трека: stop и пакет stop @ 0 (plans/app-design.md, 9.3).
    private func trackDidFinish() {
        guard status == .playing else { return }
        audio.stop()
        status = .stopped
        refresh()
        send(.stop, positionMilliseconds: 0)
    }

    // MARK: - Таймеры

    /// Каждые 0,25 с: позиция для UI. В сеть ничего не уходит.
    public func refresh() {
        position = hasTrack ? audio.currentTime : 0
    }

    /// Каждую секунду: повтор состояния в сеть.
    ///
    /// - play — позиция (раздел 2 plans/protocol.md);
    /// - stop — stop @ 0, даже без трека, как `StopTimerCallback`;
    /// - pause — ничего.
    public func networkTick() {
        guard lookup == nil else { return }
        switch status {
        case .playing:
            refresh()
            send(.play, positionMilliseconds: currentMilliseconds)
        case .stopped:
            send(.stop, positionMilliseconds: 0)
        case .paused:
            break
        }
    }

    private var currentMilliseconds: UInt32 {
        hasTrack ? TimeFormat.milliseconds(audio.currentTime) : 0
    }

    // MARK: - Сеть

    private func send(_ command: PlaybackCommand, positionMilliseconds: UInt32) {
        guard let transport else {
            networkError = "No network interface"
            return
        }
        do {
            try transport.send(command, positionMilliseconds: positionMilliseconds)
            networkError = nil
        } catch {
            // Локальное воспроизведение не прерывается.
            networkError = String(describing: error)
        }
    }

    public func reloadInterfaces() async {
        let provider = interfaceProvider
        let list = await Task.detached { provider() }.value
        applyInterfaces(list)
    }

    /// Восстановление выбора по имени. Если сохранённого интерфейса нет,
    /// берётся первый, но настройка не перезаписывается: интерфейс мог
    /// быть временно выключен.
    func applyInterfaces(_ list: [NetworkInterface]) {
        interfaces = list

        let preferred = list.first { $0.name == settings.interfaceName }
            ?? list.first { $0.id == selectedInterface?.id }
            ?? list.first

        if preferred != selectedInterface || (preferred != nil && transport == nil) {
            open(preferred)
        }
    }

    /// Выбор пользователя — запоминается.
    public func selectInterface(id: String?) {
        let interface = interfaces.first { $0.id == id }
        open(interface)
        if let interface {
            settings.interfaceName = interface.name
            store.save(settings)
        }
    }

    private func open(_ interface: NetworkInterface?) {
        transport?.close()
        transport = nil
        selectedInterface = interface
        networkError = nil

        guard let interface else { return }
        do {
            let opened = try makeTransport(interface)
            opened.onDeviceResponse = { [weak self] response in
                self?.lookup?.handle(response)
            }
            transport = opened
        } catch {
            networkError = "Cannot open UDP \(ShowConnection.port) on \(interface.name): \(error)"
        }
    }

    // MARK: - Device lookup

    /// Открывает окно поиска. `nil`, если сеть не открыта — ошибка
    /// остаётся в `networkError`.
    @discardableResult
    public func openLookup(mode: DeviceLookupModel.Mode) -> DeviceLookupModel? {
        closeLookup()
        guard let transport else {
            networkError = "No network interface"
            return nil
        }
        let model = DeviceLookupModel(mode: mode, transport: transport, makeListener: makeListener)
        lookup = model
        model.start()
        return model
    }

    public func closeLookup() {
        lookup?.close()
        lookup = nil
    }

    // MARK: - Art-Net

    /// Изменение настроек из панели. Включение, смена интерфейса —
    /// переоткрытие сокета; остальное применяется на лету.
    public func updateArtNet(_ newValue: ArtNetSettings) {
        artNet = newValue
        settings.artNet = newValue
        store.save(settings)

        guard newValue.enabled else {
            stopArtNet()
            return
        }

        if let artNetSource, artNetInterfaceName == newValue.interfaceName {
            artNetSource.update(newValue.triggerSettings)
        } else {
            startArtNet()
        }
    }

    private func startArtNet() {
        stopArtNet()

        let name = artNet.interfaceName
        let interface = name.flatMap { wanted in interfaces.first { $0.name == wanted } }
        if let name, interface == nil {
            artNetError = "Interface \(name) is not available"
            return
        }

        do {
            let source = try makeArtNet(interface, artNet.triggerSettings)
            source.onCommand = { [weak self] command in
                self?.handleDmx(command)
            }
            artNetSource = source
            artNetInterfaceName = name
            isArtNetRunning = true
            artNetError = nil
        } catch {
            // Порт занят или интерфейс пропал — выключаем, как отказ
            // `artnet.Open` в Windows-версии.
            artNetError = "Cannot open Art-Net: \(error)"
            artNet.enabled = false
            settings.artNet.enabled = false
            store.save(settings)
        }
    }

    private func stopArtNet() {
        artNetSource?.close()
        artNetSource = nil
        artNetInterfaceName = nil
        isArtNetRunning = false
    }

    /// Команда с пульта — те же действия, что кнопки. Без трека
    /// игнорируется (`player.HasAudio` в Windows-версии).
    func handleDmx(_ command: PlaybackCommand) {
        guard hasTrack else { return }
        switch command {
        case .play: play()
        case .pause: pause()
        case .stop: stop()
        case .nextTrack, .previousTrack: break
        }
    }
}
