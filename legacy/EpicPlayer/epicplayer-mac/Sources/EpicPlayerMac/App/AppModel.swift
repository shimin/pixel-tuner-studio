import EpicCore
import Foundation
import Observation

/// Состояние приложения: связано воедино аудио, сеть и настройки.
///
/// Один объект вместо разрозненных обработчиков WPF. Настройки и
/// логика воспроизведения живут в `EpicCore` и покрыты тестами; здесь
/// только их связывание с интерфейсом.
@MainActor
@Observable
final class AppModel {
    // MARK: - Настройки

    private let settings: AppSettings

    var artNet: ArtNetSettings {
        didSet {
            settings.artNet = artNet
            applyArtNet()
        }
    }

    // MARK: - Интерфейсы и устройства

    /// Интерфейсы, пригодные для связи с устройством.
    private(set) var availableInterfaces: [NetworkInterface] = []

    /// Индекс выбранного интерфейса в `availableInterfaces`.
    var selectedInterfaceIndex: Int {
        didSet {
            settings.interfaceIndex = selectedInterfaceIndex
            applyInterface()
        }
    }

    /// Устройства, найденные поиском по UDP.
    private(set) var devices: [DeviceResponse] = []

    /// Устройства, подключившиеся по TCP.
    private(set) var tcpDevices: [DeviceResponse] = []

    /// Идёт ли поиск устройств.
    private(set) var isLookingUp = false

    // MARK: - Воспроизведение

    let playback = PlaybackController()
    private let audio = AudioPlayer()

    /// Имя открытого файла.
    private(set) var songTitle = ""

    /// Позиция воспроизведения, секунды.
    private(set) var position: Double = 0

    /// Длительность трека, секунды. `nil`, пока файл не загружен.
    private(set) var duration: Double?

    /// Громкость 0...1.
    var volume: Double = 0.5 {
        didSet {
            audio.volume = volume
            if oldValue != volume { onVolumeChanged?(volume) }
        }
    }

    /// Есть ли загруженный трек с аудио.
    ///
    /// Windows-версия блокировала панель воспроизведения, если
    /// `player.HasAudio` было ложно.
    var hasMedia: Bool { audio.duration != nil }

    /// Статус для интерфейса.
    var statusText: String { playback.status.description }

    /// Подпись времени начала.
    var positionText: String { PlaybackFormatting.time(seconds: position) }

    /// Подпись полной длительности.
    var durationText: String { PlaybackFormatting.time(seconds: duration ?? 0) }

    /// Процент прослушанного для полосы прогресса.
    var progress: Double {
        PlaybackFormatting.progress(position: position, duration: duration)
    }

    /// Подпись громкости.
    var volumeText: String { PlaybackFormatting.volumeLabel(volume) }

    // MARK: - Внутреннее

    private var broadcastSocket: BroadcastSocket?
    private var udpReceiver: UdpReceiver?
    private var tcpListener: TcpListener?
    private var artNetReceiver: ArtNetReceiver?
    private var dmxTracker = DmxCommandTracker()

    /// Вызывается при изменении громкости — чтобы связать с сетью.
    var onVolumeChanged: ((Double) -> Void)?

    init(settings: AppSettings = AppSettings()) {
        self.settings = settings
        self.artNet = settings.artNet
        self.selectedInterfaceIndex = settings.interfaceIndex
    }

    // MARK: - Запуск

    /// Подготовка к работе: интерфейсы, приёмники, последний трек.
    func start() {
        refreshInterfaces()

        audio.volume = volume

        // Последний файл открывается только если он ещё существует:
        // иначе приложение стартовало бы с ошибкой.
        if let url = settings.existingFileURL {
            openFile(at: url)
        }

        applyArtNet()
        startPositionTimer()
    }

    /// Остановка приложения: таймеры и сокеты закрываются.
    func shutdown() {
        positionTimer?.invalidate()
        positionTimer = nil
        tcpListener?.stop()
        tcpListener = nil
        artNetReceiver?.stop()
        artNetReceiver = nil
        broadcastSocket?.close()
        broadcastSocket = nil
    }

    // MARK: - Интерфейсы

    func refreshInterfaces() {
        availableInterfaces = InterfaceEnumerator.usableInterfaces()

        // Индекс из настроек мог выйти за пределы списка: интерфейсы
        // меняются между запусками.
        if selectedInterfaceIndex >= availableInterfaces.count {
            selectedInterfaceIndex = availableInterfaces.isEmpty
                ? 0
                : availableInterfaces.count - 1
        } else if selectedInterfaceIndex < 0 {
            selectedInterfaceIndex = 0
        }

        applyInterface()
    }

    var selectedInterface: NetworkInterface? {
        guard availableInterfaces.indices.contains(selectedInterfaceIndex) else { return nil }
        return availableInterfaces[selectedInterfaceIndex]
    }

    private func applyInterface() {
        playback.setInterface(selectedInterface)

        // Интерфейс без адреса или широковещательного адреса непригоден:
        // сокет к нему не привязать.
        guard let iface = selectedInterface,
              iface.primaryAddress != nil,
              iface.broadcastAddress != nil else { return }

        do {
            let socket = BroadcastSocket()
            try socket.open(interface: iface, sourcePort: settings.wifiPort)
            broadcastSocket = socket
        } catch {
            // Привязка не удалась: команды отправляться не будут,
            // интерфейс остаётся выбранным, а статус покажет причину.
            broadcastSocket = nil
            statusError = error.localizedDescription
        }
    }

    /// Текст последней сетевой ошибки, показывается в интерфейсе.
    private(set) var statusError: String?

    // MARK: - Файл

    /// Открыть аудиофайл.
    func openFile(at url: URL) {
        do {
            try audio.open(url: url)
            songTitle = url.lastPathComponent
            settings.filePath = url.path
            statusError = nil

            playback.status = .play
            playback.resetPositionClock()
            audio.play()
        } catch {
            // Windows-версия при ошибке оставляла панель выключенной
            // и показывала метку «No media available».
            audio.close()
            duration = nil
            songTitle = ""
            statusError = error.localizedDescription
        }
        refreshDuration()
    }

    private func refreshDuration() {
        duration = audio.duration
        position = audio.position
    }

    // MARK: - Команды

    func play() {
        guard hasMedia else { return }
        playback.play(positionMilliseconds: UInt32(position * 1000))
        audio.play()
    }

    func pause() {
        guard hasMedia else { return }
        let milliseconds = UInt32(position * 1000)
        playback.pause(positionMilliseconds: milliseconds)
        audio.pause()
    }

    func stop() {
        let milliseconds = UInt32(position * 1000)
        playback.stop(positionMilliseconds: milliseconds)
        audio.stop()
        position = 0
    }

    /// Команды «предыдущий» и «следующий» работают только на устройстве:
    /// локальный проигрыватель трек не переключает, см. README.
    func previousTrack() {
        playback.send(.previousTrack)
    }

    func nextTrack() {
        playback.send(.nextTrack)
    }

    /// Перемотка. Позиция уходит на устройство следующим тиком.
    func seek(to seconds: Double) {
        guard hasMedia else { return }
        position = seconds
        audio.position = seconds
        playback.resetPositionClock()
    }

    // MARK: - Таймер позиции

    private var positionTimer: Timer?

    /// Таймер обновляет позицию раз в секунду и отправляет её устройству.
    ///
    /// Режим run loop — `.common`: в `.default` таймер замирает, пока
    /// пользователь тянет меню, и позиция перестаёт уходить на устройство.
    private func startPositionTimer() {
        positionTimer?.invalidate()
        positionTimer = Timer.scheduledTimer(
            withTimeInterval: 1.0,
            repeats: true) { [weak self] _ in
            // Обновление UI — на главном акторе.
            MainActor.assumeIsolated {
                self?.tickPosition()
            }
        }
        if let timer = positionTimer {
            RunLoop.main.add(timer, forMode: .common)
        }
    }

    private func tickPosition() {
        guard hasMedia else { return }
        position = audio.position

        // Позиция уходит на устройство раз в секунду, см.
        // plans/protocol.md.
        playback.tick(positionMilliseconds: UInt32(position * 1000))
    }

    // MARK: - Поиск устройств

    /// Поиск устройств по UDP.
    func startDeviceLookup() {
        devices.removeAll()
        isLookingUp = true

        startUDPReceiver()

        guard selectedInterface != nil else { return }
        broadcastSocket?.destinationPort = settings.wifiPort
        try? broadcastSocket?.send(DeviceDiscovery.udpQuery)
    }

    private func startUDPReceiver() {
        udpReceiver?.stop()

        let receiver = UdpReceiver(port: settings.wifiPort)
        receiver.onPacket = { [weak self] packet, address in
            let response = DeviceResponse(
                address: address,
                tag: ArtNetParser.deviceTag(packet),
                rawData: packet)

            MainActor.assumeIsolated {
                self?.addDevice(response, toUDP: true)
            }
        }
        try? receiver.start()
        udpReceiver = receiver
    }

    private func addDevice(_ response: DeviceResponse, toUDP: Bool) {
        if toUDP {
            if !devices.contains(where: { $0.address == response.address }) {
                devices.append(response)
            }
        } else {
            if !tcpDevices.contains(where: { $0.address == response.address }) {
                tcpDevices.append(response)
            }
        }
    }

    /// Повторный запрос: Refresh в окне устройств.
    func refreshDeviceLookup() {
        broadcastSocket?.destinationPort = settings.wifiPort
        try? broadcastSocket?.send(DeviceDiscovery.udpQuery)
    }

    /// Поиск устройств по TCP: объявление с адресом и портом.
    func startTcpDeviceLookup() {
        tcpDevices.removeAll()
        isLookingUp = true

        // Без адреса интерфейса объявление собрать нечем: устройство
        // подключится по этому адресу.
        guard let address = selectedInterface?.primaryAddress else { return }

        startTCPListener()

        broadcastSocket?.destinationPort = settings.wifiPort
        try? broadcastSocket?.send(
            DeviceDiscovery.encodeTCPAnnouncement(localAddress: address))
    }

    private func startTCPListener() {
        tcpListener?.stop()
        tcpConnections.removeAll()

        let listener = TcpListener()
        listener.onConnection = { [weak self] connection in
            let address = connection.deviceAddress

            MainActor.assumeIsolated {
                self?.tcpConnections[address] = connection
                self?.addDevice(DeviceResponse(address: address, tag: nil,
                                              rawData: Data()),
                                toUDP: false)
            }
        }
        try? listener.start()
        tcpListener = listener
    }

    /// Управление устройством из окна поиска.
    ///
    /// Ответ идёт по TCP в то соединение, с которого устройство
    /// обратилось: в объявлении указан другой порт, см. plans/protocol.md.
    func send(toTCPDevice command: PlaybackCommand, address: IPv4Address) {
        guard let connection = tcpConnections[address] else { return }
        let packet = PlaybackPacket.encode(
            command, positionMilliseconds: UInt32(position * 1000))
        try? connection.send(packet)
    }

    /// Отправка команды устройству по UDP, как делает Windows-версия
    /// в окне списка устройств.
    func send(toUDPDevice command: PlaybackCommand) {
        playback.send(command, positionMilliseconds: UInt32(position * 1000))
    }

    /// Соединения по TCP по адресу устройства.
    private var tcpConnections: [IPv4Address: TcpConnection] = [:]

    // MARK: - Art-Net

    /// Включение или выключение режима Art-Net.
    func applyArtNet() {
        artNetReceiver?.stop()
        artNetReceiver = nil

        guard artNet.isEnabled else { return }

        let receiver = ArtNetReceiver()
        receiver.onDMX = { [weak self] packet, _ in
            MainActor.assumeIsolated {
                self?.handleDMX(packet)
            }
        }

        do {
            try receiver.start()
            artNetReceiver = receiver
            statusError = nil
        } catch {
            artNet.isEnabled = false
            statusError = "Art-Net: \(error.localizedDescription)"
        }
    }

    /// Реакция на значение DMX-канала с пульта.
    ///
    /// Действие выполняется только при изменении значения: удержание
    /// кнопки не перезапускает трек многократно.
    private func handleDMX(_ packet: ArtNetDMXPacket) {
        guard artNet.channel >= 0,
              artNet.channel < packet.dmxData.count else { return }

        let value = packet.dmxData[artNet.channel]
        // Запоминаем значение до проверки: update сравнивает его
        // с предыдущим, поэтому порядок важен.
        let previous = dmxTracker.lastKnownValue
        dmxTracker.update(value: value, last: previous,
                          thresholds: artNet.thresholds)
        dmxTracker.remember(value)

        switch dmxTracker.pending {
        case .play:
            play()
        case .pause:
            pause()
        case .stop:
            stop()
        case nil:
            break
        }
    }
}