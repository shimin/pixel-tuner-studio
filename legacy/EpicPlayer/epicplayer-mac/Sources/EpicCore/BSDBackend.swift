import Darwin
import Foundation

/// Реализация `DatagramSocket` поверх BSD-сокетов.
public final class BSDDatagramSocket: DatagramSocket {
    private var descriptor: Int32 = -1

    /// Уже был отправлен echo-запрос — чтобы не отбрасывать второй.
    public private(set) var localPort: UInt16 = 0
    public private(set) var localAddress: IPv4Address?

    public init() {}

    deinit {
        close()
    }

    public func bind(port: UInt16, to address: IPv4Address?) throws {
        close()

        let fd = socket(AF_INET, SOCK_DGRAM, 0)
        guard fd >= 0 else { throw NetworkError.socketFailed(errno) }
        descriptor = fd

        // Разрешаем повторное открытие порта: приложение может быть
        // перезапущено, и сокет предыдущего запуска ещё не освобождён.
        var reuse: Int32 = 1
        setsockopt(fd, SOL_SOCKET, SO_REUSEADDR, &reuse,
                   socklen_t(MemoryLayout<Int32>.size))
        setsockopt(fd, SOL_SOCKET, SO_REUSEPORT, &reuse,
                   socklen_t(MemoryLayout<Int32>.size))

        // Привязка к адресу интерфейса нужна для отправки: без неё
        // macOS отправит broadcast через utun, а не через выбранный
        // интерфейс, и устройство пакет не увидит.
        var bindAddress = sockaddr_in()
        bindAddress.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        bindAddress.sin_family = sa_family_t(AF_INET)
        bindAddress.sin_port = port.bigEndian
        bindAddress.sin_addr = in_addr(s_addr: address?.inAddrT ?? INADDR_ANY)

        let result = withUnsafePointer(to: &bindAddress) { pointer in
            pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                Darwin.bind(fd, $0, socklen_t(MemoryLayout<sockaddr_in>.size))
            }
        }
        guard result == 0 else {
            let code = errno
            close()
            throw NetworkError.bindFailed(address?.description ?? "INADDR_ANY", code)
        }

        var local = sockaddr_in()
        var localLength = socklen_t(MemoryLayout<sockaddr_in>.size)
        let named = withUnsafeMutablePointer(to: &local) { pointer in
            pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) { sockPointer in
                getsockname(fd, sockPointer, &localLength)
            }
        }
        if named == 0 {
            localPort = UInt16(bigEndian: local.sin_port)
            localAddress = IPv4Address(inAddrT: local.sin_addr.s_addr)
        } else {
            localPort = port
            localAddress = address
        }
    }

    public func send(_ data: Data, to destination: IPv4Address, port: UInt16) throws {
        guard descriptor >= 0 else { throw NetworkError.notConfigured }

        // Без SO_BROADCAST отправка на широковещательный адрес запрещена.
        var broadcast: Int32 = 1
        setsockopt(descriptor, SOL_SOCKET, SO_BROADCAST, &broadcast,
                   socklen_t(MemoryLayout<Int32>.size))

        var target = sockaddr_in()
        target.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        target.sin_family = sa_family_t(AF_INET)
        target.sin_port = port.bigEndian
        target.sin_addr = in_addr(s_addr: destination.inAddrT)

        let sent = data.withUnsafeBytes { buffer -> Int in
            guard let base = buffer.baseAddress else { return -1 }
            return withUnsafePointer(to: &target) { pointer in
                pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                    sendto(descriptor, base, buffer.count, 0, $0,
                           socklen_t(MemoryLayout<sockaddr_in>.size))
                }
            }
        }

        guard sent == data.count else { throw NetworkError.sendFailed(errno) }
    }

    public func receive(timeoutMilliseconds: Int32) throws -> Datagram? {
        guard descriptor >= 0 else { throw NetworkError.notConfigured }

        if timeoutMilliseconds > 0 {
            var timeout = timeval(
                tv_sec: Int(timeoutMilliseconds / 1000),
                tv_usec: Int32((timeoutMilliseconds % 1000) * 1000))
            setsockopt(descriptor, SOL_SOCKET, SO_RCVTIMEO, &timeout,
                       socklen_t(MemoryLayout<timeval>.size))
        }

        var buffer = [UInt8](repeating: 0, count: 2048)
        var sender = sockaddr_in()
        var senderLength = socklen_t(MemoryLayout<sockaddr_in>.size)

        let received = withUnsafeMutablePointer(to: &sender) { senderPointer in
            senderPointer.withMemoryRebound(to: sockaddr.self, capacity: 1) { sockPointer in
                recvfrom(descriptor, &buffer, buffer.count, 0, sockPointer, &senderLength)
            }
        }

        // Таймаут или закрытие сокета — не ошибка, просто ничего не пришло.
        if received < 0 {
            if errno == EAGAIN || errno == EWOULDBLOCK
                || errno == EBADF || errno == EINVAL {
                return nil
            }
            throw NetworkError.sendFailed(errno)
        }
        guard received > 0 else { return nil }

        return Datagram(
            data: Data(buffer[0..<received]),
            sender: IPv4Address(inAddrT: sender.sin_addr.s_addr))
    }

    public func close() {
        if descriptor >= 0 {
            shutdown(descriptor, SHUT_RDWR)
            Darwin.close(descriptor)
            descriptor = -1
        }
    }

    var isOpen: Bool { descriptor >= 0 }
}

/// Реализация `StreamListener` поверх BSD-сокетов.
public final class BSDStreamListener: StreamListener {
    private var descriptor: Int32 = -1
    public private(set) var localPort: UInt16 = 0

    public init() {}

    deinit {
        close()
    }

    public func listen(on port: UInt16) throws {
        close()

        let fd = socket(AF_INET, SOCK_STREAM, 0)
        guard fd >= 0 else { throw NetworkError.socketFailed(errno) }
        descriptor = fd

        var reuse: Int32 = 1
        setsockopt(fd, SOL_SOCKET, SO_REUSEADDR, &reuse,
                   socklen_t(MemoryLayout<Int32>.size))

        var address = sockaddr_in()
        address.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        address.sin_family = sa_family_t(AF_INET)
        address.sin_port = port.bigEndian
        address.sin_addr = in_addr(s_addr: INADDR_ANY)

        let bindResult = withUnsafePointer(to: &address) { pointer in
            pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                Darwin.bind(fd, $0, socklen_t(MemoryLayout<sockaddr_in>.size))
            }
        }
        guard bindResult == 0 else {
            let code = errno
            close()
            throw NetworkError.bindFailed("TCP listener", code)
        }

        guard Darwin.listen(fd, 8) == 0 else {
            let code = errno
            close()
            throw NetworkError.optionFailed("listen", code)
        }

        var local = sockaddr_in()
        var localLength = socklen_t(MemoryLayout<sockaddr_in>.size)
        let named = withUnsafeMutablePointer(to: &local) { pointer in
            pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) { sockPointer in
                getsockname(fd, sockPointer, &localLength)
            }
        }
        localPort = named == 0 ? UInt16(bigEndian: local.sin_port) : port
    }

    public func accept(timeoutMilliseconds: Int32) throws -> StreamConnection? {
        guard descriptor >= 0 else { throw NetworkError.notConfigured }

        if timeoutMilliseconds > 0 {
            var timeout = timeval(
                tv_sec: Int(timeoutMilliseconds / 1000),
                tv_usec: Int32((timeoutMilliseconds % 1000) * 1000))
            setsockopt(descriptor, SOL_SOCKET, SO_RCVTIMEO, &timeout,
                       socklen_t(MemoryLayout<timeval>.size))
        }

        var remote = sockaddr_in()
        var remoteLength = socklen_t(MemoryLayout<sockaddr_in>.size)

        let clientDescriptor = withUnsafeMutablePointer(to: &remote) { remotePointer in
            remotePointer.withMemoryRebound(to: sockaddr.self, capacity: 1) { sockPointer in
                Darwin.accept(descriptor, sockPointer, &remoteLength)
            }
        }

        // Таймаут или закрытие — соединения нет.
        if clientDescriptor < 0 {
            if errno == EAGAIN || errno == EWOULDBLOCK
                || errno == EBADF || errno == EINVAL {
                return nil
            }
            throw NetworkError.sendFailed(errno)
        }

        return BSDStreamConnection(
            descriptor: clientDescriptor,
            remoteAddress: IPv4Address(inAddrT: remote.sin_addr.s_addr),
            remotePort: UInt16(bigEndian: remote.sin_port))
    }

    public func close() {
        if descriptor >= 0 {
            shutdown(descriptor, SHUT_RDWR)
            Darwin.close(descriptor)
            descriptor = -1
        }
    }
}

/// Соединение поверх BSD-сокета.
public final class BSDStreamConnection: StreamConnection {
    private let descriptor: Int32
    public var remoteAddress: IPv4Address
    public var remotePort: UInt16

    public init(descriptor: Int32, remoteAddress: IPv4Address, remotePort: UInt16) {
        self.descriptor = descriptor
        self.remoteAddress = remoteAddress
        self.remotePort = remotePort
    }

    deinit {
        Darwin.close(descriptor)
    }

    public func send(_ data: Data) throws {
        let sent = data.withUnsafeBytes { buffer -> Int in
            guard let base = buffer.baseAddress else { return -1 }
            return Darwin.send(descriptor, base, buffer.count, 0)
        }
        guard sent == data.count else { throw NetworkError.sendFailed(errno) }
    }

    public func receive(timeoutMilliseconds: Int32) throws -> Data? {
        if timeoutMilliseconds > 0 {
            var timeout = timeval(
                tv_sec: Int(timeoutMilliseconds / 1000),
                tv_usec: Int32((timeoutMilliseconds % 1000) * 1000))
            setsockopt(descriptor, SOL_SOCKET, SO_RCVTIMEO, &timeout,
                       socklen_t(MemoryLayout<timeval>.size))
        }

        var buffer = [UInt8](repeating: 0, count: 512)
        let received = Darwin.recv(descriptor, &buffer, buffer.count, 0)
        guard received > 0 else { return nil }
        return Data(buffer[0..<received])
    }

    public func close() {
        Darwin.close(descriptor)
    }
}