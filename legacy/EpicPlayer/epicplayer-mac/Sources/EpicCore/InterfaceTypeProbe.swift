import Foundation
import Network

// Отдельный файл: импорт Network виден только здесь. В Network есть свой
// тип IPv4Address, который иначе пересекается с EpicCore.IPv4Address.

/// Типы интерфейсов по данным `NWPathMonitor`.
///
/// На macOS Wi-Fi (`en0`) по флагам не отличить от Ethernet, а
/// `NWInterface.InterfaceType` различает их корректно.
enum InterfaceTypeProbe {
    static func currentKinds(timeout: TimeInterval) -> [String: InterfaceKind] {
        let monitor = NWPathMonitor()
        let kinds = Locked<[String: InterfaceKind]?>(nil)
        let received = DispatchSemaphore(value: 0)

        monitor.pathUpdateHandler = { path in
            var result: [String: InterfaceKind] = [:]
            for interface in path.availableInterfaces {
                result[interface.name] = InterfaceKind(interface.type)
            }
            kinds.set(result)
            received.signal()
        }
        monitor.start(queue: DispatchQueue(label: "com.epicplayer.interface-probe"))
        _ = received.wait(timeout: .now() + timeout)
        monitor.cancel()

        return kinds.get() ?? [:]
    }
}

extension InterfaceKind {
    init(_ type: NWInterface.InterfaceType) {
        switch type {
        case .wiredEthernet: self = .wiredEthernet
        case .wifi: self = .wifi
        case .cellular, .loopback, .other: self = .other
        @unknown default: self = .other
        }
    }
}
