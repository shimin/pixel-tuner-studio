import Foundation

/// Приём Art-Net DMX на порту 6454.
///
/// Приложение только слушает и не отправляет DMX. Реакция на значения
/// канала описана в `DmxCommandTracker`.
public final class ArtNetReceiver {
    private let receiver: UdpReceiver

    /// Вызывается при разборе каждого пакета ArtDmx.
    public var onDMX: ((ArtNetDMXPacket, IPv4Address) -> Void)?

    public init(socketFactory: SocketFactory = .system) {
        receiver = UdpReceiver(port: ArtNet.port, socketFactory: socketFactory)
        receiver.onPacket = { [weak self] packet, address in
            // Пакеты Art-Net начинаются с "Art-Net\0", а не с маркера
            // устройства, поэтому отбрасываются только наши собственные.
            guard !ArtNetParser.isOwnEcho(packet),
                  let parsed = ArtNetParser.parse(packet) else { return }
            self?.onDMX?(parsed, address)
        }
    }

    deinit {
        stop()
    }

    public func start() throws {
        try receiver.start()
    }

    public func stop() {
        receiver.stop()
    }

    public var isRunning: Bool { receiver.isRunning }
}