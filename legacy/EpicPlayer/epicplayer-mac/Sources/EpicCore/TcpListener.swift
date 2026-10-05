import Foundation

/// Приём TCP-соединения от устройства шоу.
///
/// Устройство читает из 9-байтового объявления адрес и порт, затем
/// подключается TCP-клиентом. Приложение принимает соединение и отвечает
/// на **тот порт, с которого пришло соединение**, а не на объявленный
/// `3331` — так же, как в Windows-версии, см.
/// `WifiTcpDevicesWindow.xaml.cs:111-133`.
public final class TcpListener {
    private let socketFactory: SocketFactory
    private var listener: StreamListener?
    private var thread: Thread?
    private let pollIntervalMilliseconds: Int32

    /// Вызывается при подключении устройства.
    public var onConnection: ((TcpConnection) -> Void)?

    public init(socketFactory: SocketFactory = .system,
                pollIntervalMilliseconds: Int32 = 200) {
        self.socketFactory = socketFactory
        self.pollIntervalMilliseconds = pollIntervalMilliseconds
    }

    deinit {
        stop()
    }

    /// Порт, на котором слушаем. Заполняется после `start()`.
    public private(set) var listeningPort: UInt16 = 0

    public var isRunning: Bool { listener != nil }

    /// Начинает слушать. Порт 0 — выбрать системой.
    public func start() throws {
        stop()

        let listener = socketFactory.makeListener()
        try listener.listen(on: 0)

        self.listener = listener
        listeningPort = listener.localPort

        let thread = Thread { [weak self] in
            self?.acceptLoop()
        }
        thread.name = "epic.tcp.accept"
        self.thread = thread
        thread.start()
    }

    public func stop() {
        listener?.close()
        listener = nil
        listeningPort = 0
        thread = nil
    }

    /// Принимает одно соединение. Используется в тестах.
    public func pollOnce() -> TcpConnection? {
        guard let listener = listener,
              let connection = try? listener.accept(
                timeoutMilliseconds: pollIntervalMilliseconds) else { return nil }
        return TcpConnection(connection)
    }

    private func acceptLoop() {
        while isRunning {
            guard let connection = pollOnce() else { continue }
            onConnection?(connection)
        }
    }
}

/// Подключение устройства, установленное по TCP.
public final class TcpConnection {
    /// Порт, с которого устройство подключилось: ответ нужно слать туда.
    public var callbackPort: UInt16 { connection.remotePort }

    /// Адрес устройства.
    public var deviceAddress: IPv4Address { connection.remoteAddress }

    private let connection: StreamConnection

    init(_ connection: StreamConnection) {
        self.connection = connection
    }

    /// Отправляет команду устройству.
    ///
    /// Windows-версия отвечала в сокет, подключённый к порту, с которого
    /// пришло соединение, см. `WifiInfoDevice.OnPlay`.
    public func send(_ data: Data) throws {
        try connection.send(data)
    }

    /// Читает непрочитанные данные, если устройство что-то прислало.
    public func receive(timeoutMilliseconds: Int32 = 200) -> Data? {
        try? connection.receive(timeoutMilliseconds: timeoutMilliseconds)
    }
}