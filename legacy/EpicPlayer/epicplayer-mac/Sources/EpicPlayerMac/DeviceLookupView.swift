import EpicCore
import EpicPlayerKit
import SwiftUI

/// Окна `WifiDevicesWindow` и `WifiTcpDevicesWindow` в одном листе.
struct DeviceLookupView: View {
    let model: DeviceLookupModel
    let onClose: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(model.mode.title).font(.headline)

            HStack {
                Text(model.totalTitle)
                Spacer()
                if model.mode == .tcp {
                    Picker("Announce", selection: announceBinding) {
                        Text("Own address").tag(TCPAnnounceAddress.interfaceAddress)
                        Text("Broadcast (as Windows)").tag(TCPAnnounceAddress.broadcastAddress)
                    }
                    .fixedSize()
                    .help("Address in the 9-byte announcement")
                    Button("Announce") { model.announce() }
                }
                Button("Refresh") { model.refresh() }
            }

            List(model.devices) { device in
                row(device)
            }
            .overlay {
                if model.devices.isEmpty {
                    Text("Waiting for devices…").foregroundStyle(.secondary)
                }
            }

            if let error = model.error {
                Text(error).font(.caption).foregroundStyle(Theme.error)
            }

            HStack {
                Spacer()
                Button("Close", action: onClose)
                    .keyboardShortcut(.cancelAction)
            }
        }
        .padding()
        .frame(minWidth: 520, minHeight: 320)
    }

    private func row(_ device: DeviceLookupModel.Device) -> some View {
        HStack {
            Text(verbatim: "IP: \(device.address)")
                .monospacedDigit()
            if model.mode == .tcp {
                Text(verbatim: ":\(device.port)").foregroundStyle(.secondary)
            }
            Text(verbatim: "Tag: \(device.name ?? "—")")
            Spacer()
            Button("Play") { model.play(device) }
            Button("Stop") { model.stop(device) }
        }
        .help(device.rawHex ?? "")
    }

    private var announceBinding: Binding<TCPAnnounceAddress> {
        Binding(get: { model.announceAddress }, set: { model.setAnnounceAddress($0) })
    }
}
