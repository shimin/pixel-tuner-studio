import Foundation

/// Приём `ArtDmx` на UDP 6454. Только слушает, DMX не отправляет.
///
/// Windows-версия биндится на IP из настроек Art-Net. На macOS такой
/// сокет не получает broadcast, поэтому здесь `0.0.0.0` + `IP_BOUND_IF`.
public final class ArtNetReceiver: @unchecked Sendable {
    public static let port: UInt16 = 6454

    private let socket: BroadcastSocket
    private let handler = Locked<((ArtDmxPacket, IPv4Address) -> Void)?>(nil)

    /// Пакет DMX и адрес пульта. Вызывается на очереди сокета, примерно
    /// 40 раз в секунду на universe — тяжёлую работу сюда не класть.
    public var onDmx: ((ArtDmxPacket, IPv4Address) -> Void)? {
        get { handler.get() }
        set { handler.set(newValue) }
    }

    /// - Parameter interface: `nil` — принимать с любого интерфейса.
    public init(interface: NetworkInterface?) throws {
        socket = try BroadcastSocket(port: Self.port, interface: interface)
        socket.onReceive = { [weak self] datagram in
            guard let self, let packet = ArtDmxPacket.parse(datagram.data) else { return }
            self.handler.get()?(packet, datagram.sender)
        }
    }

    deinit {
        socket.close()
    }

    public func close() {
        socket.close()
    }
}
