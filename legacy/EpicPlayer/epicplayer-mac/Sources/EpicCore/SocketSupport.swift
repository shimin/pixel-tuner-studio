import Darwin
import Foundation

/// Ошибка системного вызова сокета с `errno`.
public struct SocketError: Error, CustomStringConvertible {
    public let call: String
    public let code: Int32

    public var description: String {
        "\(call): \(String(cString: strerror(code))) (errno \(code))"
    }

    static func last(_ call: String) -> SocketError {
        SocketError(call: call, code: errno)
    }
}

/// Принятый UDP-пакет.
public struct Datagram {
    public let data: Data
    public let sender: IPv4Address
    public let senderPort: UInt16
}

/// Обёртки над BSD-сокетами, общие для UDP и TCP.
enum SocketSupport {
    /// `IP_BOUND_IF` из `<netinet/in.h>`: привязка сокета к интерфейсу
    /// по индексу, аналог `SO_BINDTODEVICE` в Linux. Задано числом,
    /// потому что макрос не во всех SDK виден из Swift.
    static let ipBoundInterface: Int32 = 25

    static func enable(_ fd: Int32, _ level: Int32, _ option: Int32, _ name: String) throws {
        var yes: Int32 = 1
        guard setsockopt(fd, level, option, &yes, socklen_t(MemoryLayout<Int32>.size)) == 0 else {
            throw SocketError.last("setsockopt(\(name))")
        }
    }

    /// Привязка к интерфейсу. Без неё ядро выбирает интерфейс по таблице
    /// маршрутизации, и broadcast уходит в `utun*` или Thunderbolt Bridge
    /// (подводный камень 1 в plans/porting-plan.md).
    ///
    /// Действует и на приём: сокет получает пакеты только с этого интерфейса.
    static func bind(_ fd: Int32, toInterfaceIndex index: UInt32) throws {
        var value = index
        guard setsockopt(fd, IPPROTO_IP, ipBoundInterface, &value, socklen_t(MemoryLayout<UInt32>.size)) == 0 else {
            throw SocketError.last("setsockopt(IP_BOUND_IF)")
        }
    }

    static func setNonBlocking(_ fd: Int32) throws {
        let flags = fcntl(fd, F_GETFL, 0)
        guard flags >= 0, fcntl(fd, F_SETFL, flags | O_NONBLOCK) == 0 else {
            throw SocketError.last("fcntl(O_NONBLOCK)")
        }
    }

    static func makeAddress(_ address: IPv4Address, port: UInt16) -> sockaddr_in {
        var result = sockaddr_in()
        result.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        result.sin_family = sa_family_t(AF_INET)
        result.sin_port = in_port_t(port).bigEndian
        result.sin_addr = in_addr(s_addr: address.hostOrderValue.bigEndian)
        return result
    }

    static func decode(_ address: sockaddr_in) -> (IPv4Address, UInt16) {
        (
            IPv4Address(hostOrderValue: UInt32(bigEndian: address.sin_addr.s_addr)),
            UInt16(bigEndian: address.sin_port)
        )
    }

    static func withSockaddr<Result>(
        _ address: sockaddr_in,
        _ body: (UnsafePointer<sockaddr>, socklen_t) -> Result
    ) -> Result {
        var copy = address
        return withUnsafePointer(to: &copy) { pointer in
            pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                body($0, socklen_t(MemoryLayout<sockaddr_in>.size))
            }
        }
    }

    /// Биндинг на `0.0.0.0:port`.
    ///
    /// Не на адрес интерфейса, как предполагал план: на macOS сокет,
    /// привязанный к unicast-адресу, не получает broadcast-пакеты
    /// (`x.x.x.255`). Интерфейс ограничивается через `IP_BOUND_IF`.
    static func bindAny(_ fd: Int32, port: UInt16) throws {
        let address = makeAddress(.any, port: port)
        let result = withSockaddr(address) { Darwin.bind(fd, $0, $1) }
        guard result == 0 else { throw SocketError.last("bind(:\(port))") }
    }
}
