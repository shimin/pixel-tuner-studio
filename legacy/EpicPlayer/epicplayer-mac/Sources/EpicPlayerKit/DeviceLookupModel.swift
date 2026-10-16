import EpicCore
import Foundation
import Observation

/// Окно device lookup: UDP-поиск (`WifiDevicesWindow`) или TCP-поиск
/// (`WifiTcpDevicesWindow`). См. plans/app-design.md, раздел 6.
@MainActor
@Observable
public final class DeviceLookupModel: Identifiable {
    public enum Mode: Sendable {
        case udp, tcp

        public var title: String {
            switch self {
            case .udp: return "Device lookup"
            case .tcp: return "TCP device lookup"
            }
        }
    }

    public struct Device: Identifiable, Equatable, Sendable {
        public var id: String { address.description }
        public let address: IPv4Address
        /// Куда слать команды строки: 333 в UDP-режиме, порт подключения в TCP.
        public var port: UInt16
        /// Tag из ответа устройства; в TCP-режиме неизвестен.
        public var name: String?
        /// Последний ответ целиком, hex — подсказка у строки, как в Windows.
        public var rawHex: String?
    }

    public let mode: Mode
    public private(set) var devices: [Device] = []
    public private(set) var error: String?
    /// Адрес в TCP-объявлении — до проверки на устройстве
    /// (расхождение 4 в plans/protocol.md).
    public private(set) var announceAddress: TCPAnnounceAddress = .interfaceAddress

    /// «Total: N», как в Windows.
    public var totalTitle: String { "Total: \(devices.count)" }

    private let transport: ShowTransport
    private let makeListener: CallbackListenerFactory
    @ObservationIgnored private var listener: CallbackListener?

    init(mode: Mode, transport: ShowTransport, makeListener: @escaping CallbackListenerFactory) {
        self.mode = mode
        self.transport = transport
        self.makeListener = makeListener
    }

    func start() {
        switch mode {
        case .udp:
            refresh()
        case .tcp:
            do {
                let opened = try makeListener(transport.interface)
                opened.onConnection = { [weak self] address, port in
                    self?.handleConnection(address: address, port: port)
                }
                listener = opened
            } catch {
                self.error = "Cannot listen on TCP \(DeviceDiscovery.tcpCallbackPort): \(error)"
                return
            }
            // Как в Windows: сначала UDP-запрос, затем объявление.
            refresh()
            announce()
        }
    }

    func close() {
        listener?.close()
        listener = nil
    }

    /// Кнопка Refresh — повтор `49 00 0D`.
    public func refresh() {
        perform { try transport.requestDevices() }
    }

    /// Повторить 9-байтовое объявление (TCP-режим).
    public func announce() {
        guard mode == .tcp else { return }
        perform { try transport.announceTCPCallback(using: announceAddress) }
    }

    public func setAnnounceAddress(_ value: TCPAnnounceAddress) {
        guard value != announceAddress else { return }
        announceAddress = value
        announce()
    }

    /// Play и Stop в строке: unicast, позиция 0, как `WifiInfoDevice`.
    public func play(_ device: Device) {
        perform { try transport.send(.play, positionMilliseconds: 0, to: device.address, port: device.port) }
    }

    public func stop(_ device: Device) {
        perform { try transport.send(.stop, positionMilliseconds: 0, to: device.address, port: device.port) }
    }

    /// Ответ на UDP-запрос. В TCP-режиме Windows их не показывает
    /// (приём закомментирован) — здесь так же.
    func handle(_ response: DeviceResponse) {
        guard mode == .udp else { return }

        if let index = devices.firstIndex(where: { $0.address == response.address }) {
            devices[index].rawHex = response.rawHex
            devices[index].name = response.name ?? devices[index].name
        } else {
            devices.append(Device(
                address: response.address,
                port: ShowConnection.port,
                name: response.name,
                rawHex: response.rawHex
            ))
        }
    }

    /// Повторное подключение того же устройства обновляет порт, а не
    /// добавляет строку (в Windows — дубль и исключение в словаре).
    func handleConnection(address: IPv4Address, port: UInt16) {
        if let index = devices.firstIndex(where: { $0.address == address }) {
            devices[index].port = port
        } else {
            devices.append(Device(address: address, port: port, name: nil, rawHex: nil))
        }
    }

    private func perform(_ action: () throws -> Void) {
        do {
            try action()
            error = nil
        } catch {
            self.error = String(describing: error)
        }
    }
}
