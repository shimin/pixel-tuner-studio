import Darwin
import Foundation

/// TCP-сервер для обнаружения по TCP (раздел 4 plans/protocol.md).
///
/// Приложение объявляет свой адрес и порт 3331, устройство подключается
/// клиентом. Что идёт по соединению дальше, неизвестно: в Windows-версии
/// разбор закомментирован, а `TcpListener` в `WifiTcpDevicesWindow` ни разу
/// не создаётся. Соединение отдаётся наружу как есть.
public final class TCPCallbackListener: @unchecked Sendable {
    public let port: UInt16

    private let fd: Int32
    private let queue: DispatchQueue
    private let acceptSource: DispatchSourceRead
    private let connectionHandler = Locked<((TCPDeviceConnection) -> Void)?>(nil)

    /// Новое соединение. Обработчики соединения задаются внутри колбэка,
    /// чтение начинается после его возврата. Ссылку на соединение нужно
    /// сохранить: без неё оно закрывается сразу после колбэка.
    public var onConnection: ((TCPDeviceConnection) -> Void)? {
        get { connectionHandler.get() }
        set { connectionHandler.set(newValue) }
    }

    public init(port: UInt16 = DeviceDiscovery.tcpCallbackPort, interface: NetworkInterface?) throws {
        let fd = Darwin.socket(AF_INET, SOCK_STREAM, IPPROTO_TCP)
        guard fd >= 0 else { throw SocketError.last("socket(TCP)") }

        do {
            try SocketSupport.enable(fd, SOL_SOCKET, SO_REUSEADDR, "SO_REUSEADDR")
            if let interface {
                try SocketSupport.bind(fd, toInterfaceIndex: interface.index)
            }
            try SocketSupport.setNonBlocking(fd)
            try SocketSupport.bindAny(fd, port: port)
            guard Darwin.listen(fd, 8) == 0 else { throw SocketError.last("listen(:\(port))") }
        } catch {
            Darwin.close(fd)
            throw error
        }

        self.fd = fd
        self.port = port
        self.queue = DispatchQueue(label: "com.epicplayer.tcp.\(port)")
        self.acceptSource = DispatchSource.makeReadSource(fileDescriptor: fd, queue: queue)

        acceptSource.setEventHandler { [weak self] in self?.acceptPending() }
        acceptSource.setCancelHandler { Darwin.close(fd) }
        acceptSource.resume()

        Log.network.info("TCP :\(port) слушает на \(interface?.name ?? "любом интерфейсе", privacy: .public)")
    }

    deinit {
        close()
    }

    /// Перестаёт принимать новые соединения. Уже принятые не закрываются.
    public func close() {
        if !acceptSource.isCancelled {
            acceptSource.cancel()
        }
    }

    private func acceptPending() {
        while true {
            var from = sockaddr_in()
            var length = socklen_t(MemoryLayout<sockaddr_in>.size)
            let client = withUnsafeMutablePointer(to: &from) { pointer in
                pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                    Darwin.accept(fd, $0, &length)
                }
            }

            if client < 0 {
                let code = errno
                if code == EINTR { continue }
                if code != EAGAIN && code != EWOULDBLOCK {
                    Log.network.error("accept :\(self.port): errno \(code)")
                }
                return
            }

            do {
                // Без этого запись в закрытое устройством соединение
                // убивает процесс сигналом SIGPIPE.
                try SocketSupport.enable(client, SOL_SOCKET, SO_NOSIGPIPE, "SO_NOSIGPIPE")
                try SocketSupport.setNonBlocking(client)
            } catch {
                Log.network.error("TCP-клиент: \(String(describing: error), privacy: .public)")
                Darwin.close(client)
                continue
            }

            let (address, remotePort) = SocketSupport.decode(from)
            let connection = TCPDeviceConnection(fd: client, remoteAddress: address, remotePort: remotePort)
            Log.network.info("TCP-подключение от \(address, privacy: .public):\(remotePort)")

            connectionHandler.get()?(connection)
            connection.start()
        }
    }
}

/// Соединение, открытое устройством. Ответ идёт по этому же
/// соединению, то есть на эфемерный порт устройства, а не на 3331.
public final class TCPDeviceConnection: @unchecked Sendable {
    public let remoteAddress: IPv4Address
    public let remotePort: UInt16

    private let fd: Int32
    private let queue: DispatchQueue
    private let readSource: DispatchSourceRead
    private let dataHandler = Locked<((Data) -> Void)?>(nil)
    private let closeHandler = Locked<(() -> Void)?>(nil)

    public var onData: ((Data) -> Void)? {
        get { dataHandler.get() }
        set { dataHandler.set(newValue) }
    }

    /// Устройство закрыло соединение или произошла ошибка чтения.
    public var onClose: (() -> Void)? {
        get { closeHandler.get() }
        set { closeHandler.set(newValue) }
    }

    init(fd: Int32, remoteAddress: IPv4Address, remotePort: UInt16) {
        self.fd = fd
        self.remoteAddress = remoteAddress
        self.remotePort = remotePort
        self.queue = DispatchQueue(label: "com.epicplayer.tcp.\(remoteAddress):\(remotePort)")
        self.readSource = DispatchSource.makeReadSource(fileDescriptor: fd, queue: queue)

        readSource.setEventHandler { [weak self] in self?.drain() }
        readSource.setCancelHandler { Darwin.close(fd) }
    }

    deinit {
        close()
    }

    func start() {
        readSource.resume()
    }

    public func send(_ data: Data) throws {
        var offset = 0
        while offset < data.count {
            let written = data.withUnsafeBytes { buffer in
                Darwin.send(fd, buffer.baseAddress! + offset, buffer.count - offset, 0)
            }
            if written < 0 {
                if errno == EINTR { continue }
                throw SocketError.last("send(\(remoteAddress):\(remotePort))")
            }
            offset += written
        }
        Log.network.debug("→ tcp \(self.remoteAddress, privacy: .public):\(self.remotePort) \(data.hexString, privacy: .public)")
    }

    public func close() {
        if !readSource.isCancelled {
            readSource.cancel()
        }
    }

    private func drain() {
        let capacity = 2048
        var buffer = [UInt8](repeating: 0, count: capacity)

        while true {
            let count = buffer.withUnsafeMutableBytes {
                Darwin.recv(fd, $0.baseAddress, capacity, 0)
            }

            if count > 0 {
                let data = Data(buffer[0..<count])
                Log.network.debug("← tcp \(self.remoteAddress, privacy: .public):\(self.remotePort) \(data.hexString, privacy: .public)")
                dataHandler.get()?(data)
                continue
            }

            let code = errno
            if count < 0 && code == EINTR { continue }
            if count < 0 && (code == EAGAIN || code == EWOULDBLOCK) { return }

            // 0 — устройство закрыло соединение, < 0 — ошибка.
            Log.network.info("TCP \(self.remoteAddress, privacy: .public):\(self.remotePort) закрыто")
            close()
            closeHandler.get()?()
            return
        }
    }
}
