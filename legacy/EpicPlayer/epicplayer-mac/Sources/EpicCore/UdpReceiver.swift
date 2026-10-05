import Foundation

/// Приём UDP-пакетов от устройств шоу.
///
/// Windows-версия использовала `BeginReceive`/`EndReceive` на сокете,
/// привязанном на `0.0.0.0:333`. Здесь то же самое, но приём идёт
/// циклом в отдельном потоке, а сокет берётся из `SocketFactory` —
/// поэтому логику можно проверить тестом без настоящих сокетов.
public final class UdpReceiver {
    private let socketFactory: SocketFactory
    private var socket: DatagramSocket?
    private var thread: Thread?

    /// Порт назначения приложения. Устройство отвечает на него.
    public static let defaultPort: UInt16 = 333

    /// Порт, на котором слушаем.
    public let port: UInt16

    /// Таймаут чтения: чтобы поток мог заметить остановку.
    private let pollIntervalMilliseconds: Int32

    /// Фактический порт после привязки.
    public private(set) var listeningPort: UInt16 = 0

    public var isRunning: Bool { socket != nil }

    /// Вызывается для каждого принятого пакета.
    public var onPacket: ((Data, IPv4Address) -> Void)?

    public init(port: UInt16 = UdpReceiver.defaultPort,
                socketFactory: SocketFactory = .system,
                pollIntervalMilliseconds: Int32 = 200) {
        self.port = port
        self.socketFactory = socketFactory
        self.pollIntervalMilliseconds = pollIntervalMilliseconds
    }

    deinit {
        stop()
    }

    /// Привязывает сокет, но не запускает фоновое чтение.
    ///
    /// Тесты используют её вместе с `pollOnce`: фоновый поток и тест
    /// читали бы один сокет одновременно.
    public func open() throws {
        stop()

        let socket = socketFactory.makeDatagram()
        try socket.bind(port: port, to: nil)

        self.socket = socket
        listeningPort = socket.localPort
    }

    /// Привязывает сокет и запускает чтение в отдельном потоке.
    public func start() throws {
            try open()
            startReading()
        }

        /// Запускает цикл чтения на уже привязанном сокете.
        ///
        /// Вынесено отдельно от `start()`, чтобы тест мог наполнить очередь
        /// подставного сокета ДО начала чтения.
        public func startReading() {
            let thread = Thread { [weak self] in
                self?.receiveLoop()
            }
            thread.name = "epic.udp.receive"
            self.thread = thread
            thread.start()
        }

    /// Останавливает приём и закрывает сокет.
    public func stop() {
        // Закрытие сокета разрывает блокирующее чтение в потоке.
        socket?.close()
        socket = nil
        listeningPort = 0
        thread = nil
    }

    /// Принимает один пакет. Предполагается вызов после `open()`.
    public func pollOnce() -> Datagram? {
        guard let socket = socket else { return nil }
        return try? socket.receive(timeoutMilliseconds: pollIntervalMilliseconds)
    }

    /// Разбор принятого пакета: отбрасывает чужие и разбирает ответ
    /// устройства.
    ///
    /// - Returns: устройство, если пакет является его ответом.
    public static func decodeDeviceResponse(_ datagram: Datagram) -> DeviceResponse? {
        guard ArtNetParser.isDeviceResponse(datagram.data) else { return nil }

        return DeviceResponse(
            address: datagram.sender,
            tag: ArtNetParser.deviceTag(datagram.data),
            rawData: datagram.data)
    }

    private func receiveLoop() {
        while isRunning {
            guard let socket = socket else { break }

            guard let datagram = try? socket.receive(
                timeoutMilliseconds: pollIntervalMilliseconds) else { continue }

            // Пропускаем собственные пакеты: приложение шлёт их само
            // и не должно показывать себя в списке устройств.
            if ArtNetParser.isOwnEcho(datagram.data) { continue }

            onPacket?(datagram.data, datagram.sender)
        }
    }
}

/// Ответ устройства на запрос обнаружения.
public struct DeviceResponse: Equatable {
    /// Адрес, с которого пришёл ответ.
    public let address: IPv4Address
    /// Имя устройства, если ответ достаточно длинный.
    public let tag: String?
    /// Пакет целиком — для разбора при необходимости.
    public let rawData: Data

    public init(address: IPv4Address, tag: String?, rawData: Data) {
        self.address = address
        self.tag = tag
        self.rawData = rawData
    }
}