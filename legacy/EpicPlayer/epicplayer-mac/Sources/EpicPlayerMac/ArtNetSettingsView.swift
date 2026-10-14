import EpicPlayerKit
import SwiftUI

/// Панель Art-Net. Поля Local IP, Subnet и Local Port Windows-версии
/// заменены выбором интерфейса (plans/app-design.md, раздел 7).
struct ArtNetSettingsView: View {
    let model: PlayerModel

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Toggle("Use Art-Net", isOn: field(\.enabled))

            Grid(alignment: .leading, horizontalSpacing: 8, verticalSpacing: 6) {
                GridRow {
                    Text("Interface:")
                    Picker("Interface", selection: field(\.interfaceName)) {
                        Text("Any").tag(String?.none)
                        ForEach(interfaceNames, id: \.self) { name in
                            Text(name).tag(Optional(name))
                        }
                    }
                    .labelsHidden()
                }
                GridRow {
                    Text("Universe:")
                    HStack {
                        Toggle("Any", isOn: anyUniverse)
                        if model.artNet.universe != nil {
                            TextField("Universe", value: universe, format: .number)
                                .labelsHidden()
                        }
                    }
                }
                GridRow {
                    Text("Channel:")
                    TextField("Channel", value: field(\.channel), format: .number)
                        .labelsHidden()
                        .help("DMX channel, 1–512")
                }
                GridRow {
                    Text("Start:")
                    TextField("Start", value: field(\.playValue), format: .number).labelsHidden()
                }
                GridRow {
                    Text("Pause:")
                    TextField("Pause", value: field(\.pauseValue), format: .number).labelsHidden()
                }
                GridRow {
                    Text("Stop:")
                    TextField("Stop", value: field(\.stopValue), format: .number).labelsHidden()
                }
            }
            .textFieldStyle(.roundedBorder)

            if model.isArtNetRunning {
                Text("Listening on UDP 6454").font(.caption).foregroundStyle(.secondary)
            }
            if let error = model.artNetError {
                Text(error).font(.caption).foregroundStyle(Theme.error)
            }
        }
    }

    /// Имена без повторов (на интерфейсе бывает несколько адресов), плюс
    /// сохранённое имя, даже если интерфейса сейчас нет.
    private var interfaceNames: [String] {
        var names: [String] = []
        for interface in model.interfaces where !names.contains(interface.name) {
            names.append(interface.name)
        }
        if let saved = model.artNet.interfaceName, !names.contains(saved) {
            names.append(saved)
        }
        return names
    }

    private func field<Value>(_ keyPath: WritableKeyPath<ArtNetSettings, Value>) -> Binding<Value> {
        Binding(
            get: { model.artNet[keyPath: keyPath] },
            set: { newValue in
                var settings = model.artNet
                settings[keyPath: keyPath] = newValue
                model.updateArtNet(settings)
            }
        )
    }

    private var anyUniverse: Binding<Bool> {
        Binding(
            get: { model.artNet.universe == nil },
            set: { isAny in
                var settings = model.artNet
                settings.universe = isAny ? nil : 0
                model.updateArtNet(settings)
            }
        )
    }

    private var universe: Binding<Int> {
        Binding(
            get: { model.artNet.universe ?? 0 },
            set: { newValue in
                var settings = model.artNet
                settings.universe = newValue
                model.updateArtNet(settings)
            }
        )
    }
}
