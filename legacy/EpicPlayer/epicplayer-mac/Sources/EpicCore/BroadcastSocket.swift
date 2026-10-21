import Darwin
import Foundation

/// UDP-сокет, привязанный к выбранному интерфейсу, с разрешённым broadcast.
///
/// Приём идёт на собственной последовательной очереди; `onReceive`
/// вызывается на ней же. Для UI — переходить на главный актор самостоятельно.
public final class BroadcastSocket: @unchecked Sendable {
    public let port: UInt16
    public let interface: NetworkInterface?

    private let fd: Int32
    private let queue: DispatchQueue
    private let readSource: DispatchSourceRead
    private let handler = Locked<((Datagram) -> Void)?>(nil)

    public var onReceive: ((Datagram) -> Void)? {
        get { handler.get() }
        set { handler.set(newValue) }
    }

    /// - Parameters:
    ///   - port: локальный порт; 333 для протокола шоу, 6454 для Art-Net.
    ///   - interface: интерфейс для `IP_BOUND_IF`; `nil` — любой, как
    ///     `0.0.0.0` в Windows-версии.
    public init(port: UInt16, interface: NetworkInterface?) throws {
        let fd = Darwin.socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP)
        guard fd >= 0 else { throw SocketError.last("socket(UDP)") }

        do {
            try SocketSupport.enable(fd, SOL_SOCKET, SO_REUSEADDR, "SO_REUSEADDR")
            // Второй экземпляр приложения или чужой Art-Net софт на том же
            // порту не должен мешать открыть сокет. Broadcast получат все.
            try SocketSupport.enable(fd, SOL_SOCKET, SO_REUSEPORT, "SO_REUSEPORT")
            try SocketSupport.enable(fd, SOL_SOCKET, SO_BROADCAST, "SO_BROADCAST")
            if let interface {
                try SocketSupport.bind(fd, toInterfaceIndex: interface.index)
            }
            try SocketSupport.setNonBlocking(fd)
            try SocketSupport.bindAny(fd, port: port)
        } catch {
            Darwin.close(fd)
            throw error
        }

        self.fd = fd
        self.port = port
        self.interface = interface
        self.queue = DispatchQueue(label: "com.epicplayer.udp.\(port)")
        self.readSource = DispatchSource.makeReadSource(fileDescriptor: fd, queue: queue)

        readSource.setEventHandler { [weak self] in self?.drain() }
        readSource.setCancelHandler { Darwin.close(fd) }
        readSource.resume()

        Log.network.info("UDP :\(port) открыт на \(interface?.name ?? "любом интерфейсе", privacy: .public)")
    }

    deinit {
        close()
    }

    /// Новый буфер на каждый вызов — общий буфер Windows-версии давал
    /// гонку (расхождение 3 в plans/protocol.md).
    public func send(_ data: Data, to address: IPv4Address, port: UInt16) throws {
        let destination = SocketSupport.makeAddress(address, port: port)
        let sent = data.withUnsafeBytes { buffer in
            SocketSupport.withSockaddr(destination) {
                Darwin.sendto(fd, buffer.baseAddress, buffer.count, 0, $0, $1)
            }
        }
        guard sent == data.count else { throw SocketError.last("sendto(\(address):\(port))") }
        Log.network.debug("→ \(address, privacy: .public):\(port) \(data.hexString, privacy: .public)")
    }

    /// Закрывает сокет. Повторный вызов безопасен.
    public func close() {
        if !readSource.isCancelled {
            readSource.cancel()
        }
    }

    private func drain() {
        let capacity = 2048
        var buffer = [UInt8](repeating: 0, count: capacity)

        while true {
            var from = sockaddr_in()
            var length = socklen_t(MemoryLayout<sockaddr_in>.size)
            let count = buffer.withUnsafeMutableBytes { raw in
                withUnsafeMutablePointer(to: &from) { pointer in
                    pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                        Darwin.recvfrom(fd, raw.baseAddress, capacity, 0, $0, &length)
                    }
                }
            }

            if count < 0 {
                let code = errno
                if code == EINTR { continue }
                if code != EAGAIN && code != EWOULDBLOCK {
                    Log.network.error("recvfrom :\(self.port): errno \(code)")
                }
                return
            }

            let (sender, senderPort) = SocketSupport.decode(from)
            let datagram = Datagram(data: Data(buffer[0..<count]), sender: sender, senderPort: senderPort)
            Log.network.debug("← \(sender, privacy: .public):\(senderPort) \(datagram.data.hexString, privacy: .public)")
            handler.get()?(datagram)
        }
    }
}

extension Data {
    /// `50 05 02 00 00 00 00 0D` — как `RawData` в Windows-версии.
    public var hexString: String {
        map { String(format: "%02X", $0) }.joined(separator: " ")
    }
}
