import Darwin
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
            return "Ошибка сети: \(String(cString: strerror(code)))"
        case .notConfigured:
            return "Сокет не настроен: не выбран сетевой интерфейс"
        }
    }
}

/// Отправка команд устройству шоу.
///
/// Ключевое отличие от Windows-версии: сокет привязан к явно выбранному
/// интерфейсу. Windows-код биндит сокет на `0.0.0.0` и полагается на
/// маршрутизацию — на macOS это приводит к тому, что broadcast уходит
/// в `utun` вместо выбранного интерфейса, и устройство пакет не видит.
public final class BroadcastSocket {
    private let socketFactory: SocketFactory
    private var socket: DatagramSocket?

    /// Адрес, в который уходит широковещание.
    public private(set) var destination: IPv4Address?

    /// Порт назначения. По умолчанию 333 — порт собственного протокола.
    public var destinationPort: UInt16 = 333

    public init(socketFactory: SocketFactory = .system) {
        self.socketFactory = socketFactory
    }

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

        let socket = socketFactory.makeDatagram()
        try socket.bind(port: sourcePort, to: localAddress)

        self.socket = socket
        destination = broadcast
    }

    /// Отправляет пакет на широковещательный адрес интерфейса.
    public func send(_ data: Data) throws {
        guard let socket = socket, let broadcast = destination else {
            throw NetworkError.notConfigured
        }
        try socket.send(data, to: broadcast, port: destinationPort)
    }

    /// Адрес, к которому привязан сокет.
    public var localAddress: IPv4Address? { socket?.localAddress }

    public var isOpen: Bool { socket != nil }

    public func close() {
        socket?.close()
        socket = nil
        destination = nil
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

    init(inAddrT: in_addr_t) {
        self.init([UInt8((inAddrT >> 24) & 0xFF),
                   UInt8((inAddrT >> 16) & 0xFF),
                   UInt8((inAddrT >> 8) & 0xFF),
                   UInt8(inAddrT & 0xFF)])
    }
}