import Foundation

/// Ошибки сетевого слоя.
public enum NetworkError: Error, CustomStringConvertible {
    case socketFailed(Int32)
    case bindFailed(String, Int32)
    case optionFailed(String, Int32)
    case sendFailed(Int32)
    case notConfigured

    public var description: String {
        switch self {
        case .socketFailed(let code):
            return "Не удалось создать сокет: \(String(cString: strerror(code)))"
        case .bindFailed(let address, let code):
            return "Не удалось привязать сокет к \(address): \(String(cString: strerror(code)))"
        case .optionFailed(let name, let code):
            return "Не удалось установить параметр сокета \(name): \(String(cString: strerror(code)))"
        case .sendFailed(let code):
            return "Ошибка отправки: \(String(cString: strerror(code)))"
        case .notConfigured:
            return "Сокет не настроен: не выбран сетевой интерфейс"
        }
    }
}

/// UDP-сокет для отправки команд устройству.
///
/// Ключевое отличие от Windows-версии: сокет привязан к явно выбранному
/// интерфейсу. Windows-код биндит сокет на `0.0.0.0` и полагается на
/// маршрутизацию — на macOS это приводит к тому, что broadcast уходит
/// в `utun` вместо выбранного интерфейса, и устройство пакет не видит.
public final class BroadcastSocket {
    private var descriptor: Int32 = -1
    private var boundAddress: IPv4Address?
    private var boundPort: UInt16 = 0

    /// Адрес, в который уходит broadcast.
    public private(set) var destination: IPv4Address?

    public init() {}

    deinit {
        close()
    }

    /// Открывает сокет, привязанный к интерфейсу.
    ///
    /// - Parameter sourcePort: локальный порт для привязки. Windows-версия
    ///   использует 333 — тот же порт, что и у получателя, чтобы
    ///   устройство могло отвечать на него.
    public func open(interface: NetworkInterface, sourcePort: UInt16) throws {
        guard let localAddress = interface.primaryAddress,
              let broadcast = interface.broadcastAddress else {
            throw NetworkError.notConfigured
        }

        close()

        let fd = socket(AF_INET, SOCK_DGRAM, 0)
        guard fd >= 0 else { throw NetworkError.socketFailed(errno) }
        descriptor = fd

        // Без SO_BROADCAST отправка на широковещательный адрес запрещена.
        try setOption(SO_BROADCAST, value: 1, name: "SO_BROADCAST")

        // Привязка к адресу интерфейса: пакеты пойдут через него, а не
        // по маршруту по умолчанию. Дополнительно задаём SO_BINDTODEVICE,
        // где платформа это поддерживает.
        var bindAddress = sockaddr_in()
        bindAddress.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        bindAddress.sin_family = sa_family_t(AF_INET)
        bindAddress.sin_port = sourcePort.bigEndian
        bindAddress.sin_addr = in_addr(s_addr: localAddress.inAddrT)

        let bindResult = withUnsafePointer(to: &bindAddress) { pointer in
            pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                bind(fd, $0, socklen_t(MemoryLayout<sockaddr_in>.size))
            }
        }
        guard bindResult == 0 else {
            let code = errno
            close()
            throw NetworkError.bindFailed(localAddress.description, code)
        }

        boundAddress = localAddress
        boundPort = sourcePort
        destination = broadcast
    }

    /// Отправляет пакет на широковещательный адрес интерфейса.
    public func send(_ data: Data) throws {
        guard descriptor >= 0, let broadcast = destination else {
            throw NetworkError.notConfigured
        }

        var target = sockaddr_in()
        target.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        target.sin_family = sa_family_t(AF_INET)
        target.sin_port = destinationPort.bigEndian
        target.sin_addr = in_addr(s_addr: broadcast.inAddrT)

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

    /// Порт назначения. По умолчанию 333 — порт собственного протокола.
    /// Для Art-Net вызывающий код задаёт 6454.
    public var destinationPort: UInt16 = 333

    public func close() {
        if descriptor >= 0 {
            // Имённо системный close: метод с таким же названием есть
            // у самого класса.
            Darwin.close(descriptor)
            descriptor = -1
        }
        boundAddress = nil
        boundPort = 0
        destination = nil
    }

    /// Адрес, к которому привязан сокет.
    public var localAddress: IPv4Address? { boundAddress }

    private func setOption(_ option: Int32, value: Int32, name: String) throws {
        var value = value
        let result = setsockopt(descriptor, SOL_SOCKET, option, &value,
                               socklen_t(MemoryLayout<Int32>.size))
        guard result == 0 else { throw NetworkError.optionFailed(name, errno) }
    }
}

public extension IPv4Address {
    /// Байты адреса в сетевом порядке для `sockaddr_in`.
    ///
    /// `sockaddr_in.sin_addr.s_addr` имеет тип `in_addr_t` (UInt32),
    /// поэтому возвращается число, а не `in_addr`.
    var inAddrT: in_addr_t {
        in_addr_t(octets[0])
            | (in_addr_t(octets[1]) << 8)
            | (in_addr_t(octets[2]) << 16)
            | (in_addr_t(octets[3]) << 24)
    }

    init?(inAddrT: in_addr_t) {
        self.init([UInt8((inAddrT >> 24) & 0xFF),
                   UInt8((inAddrT >> 16) & 0xFF),
                   UInt8((inAddrT >> 8) & 0xFF),
                   UInt8(inAddrT & 0xFF)])
    }
}