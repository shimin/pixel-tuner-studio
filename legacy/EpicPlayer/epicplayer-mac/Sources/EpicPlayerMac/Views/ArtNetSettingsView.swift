import EpicCore
import SwiftUI

/// Настройки Art-Net.
///
/// Поля повторяют блок «ArtNet settings» из `MainWindow.xaml`.
/// Значения по умолчанию — из настроек Windows-версии, а не из XAML:
/// там stop и pause переставлены местами, см. `ArtNetSettings`.
struct ArtNetSettingsView: View {
    @Bindable var model: AppModel
    @Binding var isExpanded: Bool

    var body: some View {
        DisclosureGroup("ArtNet settings", isExpanded: $isExpanded) {
            VStack(alignment: .leading, spacing: 8) {
                Toggle("Use Artnet", isOn: enabledBinding)
                    .toggleStyle(.checkbox)

                field("Local IP:", value: stringBinding(\.localIP))
                field("Subnet:", value: stringBinding(\.subnetMask))
                field("Local Port:", value: stringBinding(\.port))
                field("Universe:", value: stringBinding(\.universe))
                field("Channel:", value: channelBinding)
                field("Start:", value: valueBinding(\.start))
                field("Stop:", value: valueBinding(\.stop))
                field("Pause:", value: valueBinding(\.pause))
            }
            .padding(.top, 8)
        }
        .padding(8)
    }

    private var enabledBinding: Binding<Bool> {
        Binding(
            get: { model.artNet.isEnabled },
            set: { model.artNet.isEnabled = $0 })
    }

    private func stringBinding(_ keyPath: WritableKeyPath<ArtNetSettings, String>)
        -> Binding<String> {
        Binding(
            get: { model.artNet[keyPath: keyPath] },
            set: { model.artNet[keyPath: keyPath] = $0 })
    }

    private func valueBinding(_ keyPath: WritableKeyPath<ArtNetSettings, UInt8>)
        -> Binding<String> {
        Binding(
            get: { String(model.artNet[keyPath: keyPath]) },
            set: { text in
                if let value = UInt8(text) {
                    model.artNet[keyPath: keyPath] = value
                }
            })
    }

    /// Номер канала DMX, отсчитывается с нуля.
    private var channelBinding: Binding<String> {
        Binding(
            get: { String(model.artNet.channel) },
            set: { text in
                if let value = Int(text), value >= 0 {
                    model.artNet.channel = value
                }
            })
    }

    private func field(_ label: String, value: Binding<String>) -> some View {
        HStack(spacing: 6) {
            Text(label)
                .frame(width: 80, alignment: .leading)
            TextField("", text: value)
                .textFieldStyle(.roundedBorder)
        }
    }
}