import EpicCore
import SwiftUI

/// Окно списка найденных устройств.
///
/// Повторяет `WifiDevicesWindow.xaml` и `WifiTcpDevicesWindow.xaml`:
/// счётчик найденных, список с адресом и именем, кнопки Play и Stop
/// у каждого устройства, Refresh и Close.
struct DeviceLookupView: View {
    enum Mode {
        /// Поиск по UDP.
        case udp
        /// Устройства подключились сами по TCP.
        case tcp

        var title: String {
            switch self {
            case .udp: return "Wifi Devices"
            case .tcp: return "Wifi Devices TCP"
            }
        }
    }

    @Bindable var model: AppModel
    let mode: Mode

    @Environment(\.dismiss) private var dismiss

    var body: some View {
        VStack(spacing: 0) {
            header
            Divider()
            deviceList
            Divider()
            footer
        }
        .frame(minWidth: 420, minHeight: 420)
        .background(Color(red: 0.1, green: 0.1, blue: 0.1))
        .foregroundStyle(.white)
    }

    private var devices: [DeviceResponse] {
        switch mode {
        case .udp: return model.devices
        case .tcp: return model.tcpDevices
        }
    }

    // MARK: - Шапка

    private var header: some View {
        HStack {
            Text("Total: \(devices.count)")
                .padding(.leading, 8)
            Spacer()
            Button("Refresh") {
                switch mode {
                case .udp: model.refreshDeviceLookup()
                case .tcp: break
                }
            }
            .buttonStyle(.bordered)
            .padding(.trailing, 8)
        }
        .padding(.vertical, 6)
    }

    // MARK: - Список

    private var deviceList: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 4) {
                if devices.isEmpty {
                    Text("No devices found")
                        .foregroundStyle(.secondary)
                        .padding(10)
                } else {
                    ForEach(Array(devices.enumerated()), id: \.offset) { _, device in
                        deviceRow(device)
                    }
                }
            }
            .padding(.horizontal, 8)
        }
        .frame(maxHeight: 320)
    }

    private func deviceRow(_ device: DeviceResponse) -> some View {
        HStack(spacing: 6) {
            Text("IP:")
            Text(device.address.description)
                .monospacedDigit()
                .frame(width: 130, alignment: .leading)

            Text("Tag:")
            Text(device.tag ?? "—")
                .lineLimit(1)
                .frame(maxWidth: .infinity, alignment: .leading)
                .help(device.tag ?? "")

            Button("Play") { send(.play, to: device) }
                .buttonStyle(.bordered)
                .controlSize(.small)
            Button("Stop") { send(.stop, to: device) }
                .buttonStyle(.bordered)
                .controlSize(.small)
        }
        .padding(.vertical, 2)
    }

    /// Команда устройству: по TCP — в то соединение, с которого оно
    /// обратилось, по UDP — широковещательно.
    private func send(_ command: PlaybackCommand, to device: DeviceResponse) {
        switch mode {
        case .udp:
            model.send(toUDPDevice: command)
        case .tcp:
            model.send(toTCPDevice: command, address: device.address)
        }
    }

    // MARK: - Подвал

    private var footer: some View {
        HStack {
            Spacer()
            Button("Close") { dismiss() }
                .buttonStyle(.bordered)
                .frame(width: 80)
        }
        .padding(8)
    }
}