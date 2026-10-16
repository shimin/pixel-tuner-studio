import SwiftUI

/// Главное окно. Компоновка повторяет `MainWindow.xaml` Windows-версии:
/// выбор интерфейса и поиск устройств слева, транспорт по центру,
/// настройки Art-Net справа.
struct ContentView: View {
    @State private var model = AppModel()
    @State private var showUDPWindow = false
    @State private var showTCPWindow = false
    @State private var showArtNet = false
    @State private var isImporting = false

    var body: some View {
        HStack(alignment: .top, spacing: 0) {
            leftColumn
                .frame(maxWidth: .infinity)

            transportColumn
                .frame(maxWidth: .infinity)
                .disabled(!model.hasMedia)
                .overlay {
                    if !model.hasMedia {
                        Text("No media available")
                            .foregroundStyle(Color(red: 0.8 as Double, green: 0.2 as Double, blue: 0.2 as Double))
                    }
                }

            ArtNetSettingsView(model: model, isExpanded: $showArtNet)
                .frame(width: 240)
        }
        .padding(.vertical, 8)
        .background(Color(red: 0.125, green: 0.125, blue: 0.125))
        .foregroundStyle(.white)
        .frame(minWidth: 720, minHeight: 320)
        .task { model.start() }
        .onDisappear { model.shutdown() }
        .fileImporter(
            isPresented: $isImporting,
            allowedContentTypes: [.audio, .mp3, .wav],
            allowsMultipleSelection: false
        ) { result in
            if case .success(let urls) = result, let url = urls.first {
                model.openFile(at: url)
            }
        }
        .sheet(isPresented: $showUDPWindow) {
            DeviceLookupView(model: model, mode: .udp)
        }
        .sheet(isPresented: $showTCPWindow) {
            DeviceLookupView(model: model, mode: .tcp)
        }
    }

    // MARK: - Левая колонка

    private var leftColumn: some View {
        VStack(spacing: 6) {
            Button("Open audio") { isImporting = true }
                .buttonStyle(.bordered)
                .controlSize(.large)

            Picker("", selection: Binding(
                get: { model.selectedInterfaceIndex },
                set: { model.selectedInterfaceIndex = $0 }
            )) {
                if model.availableInterfaces.isEmpty {
                    Text("No interface").tag(0)
                } else {
                    ForEach(Array(model.availableInterfaces.enumerated()), id: \.element.id) {
                        index, iface in
                        Text(iface.displayName).tag(index)
                    }
                }
            }
            .labelsHidden()
            .onChange(of: model.selectedInterfaceIndex) { _, _ in
                model.refreshDeviceLookup()
            }

            Button("Device lookup") {
                model.startDeviceLookup()
                showUDPWindow = true
            }
            .buttonStyle(.bordered)
            .disabled(model.availableInterfaces.isEmpty)

            Button("Tcp Device lookup") {
                model.startTcpDeviceLookup()
                showTCPWindow = true
            }
            .buttonStyle(.bordered)
            .disabled(model.availableInterfaces.isEmpty)

            Text(model.songTitle)
                .frame(maxWidth: .infinity, alignment: .leading)

            ProgressView(value: model.progress, total: 100)
                .progressViewStyle(.linear)
                .tint(Color(red: 0.16 as Double, green: 0.93 as Double, blue: 0.30 as Double))

            HStack {
                Text(model.positionText)
                Spacer()
                Text(model.durationText)
            }

            if let error = model.statusError {
                Text(error)
                    .font(.callout)
                    .foregroundStyle(Color(red: 0.8 as Double, green: 0.2 as Double, blue: 0.2 as Double))
            }
        }
        .padding(.horizontal, 8)
    }

    // MARK: - Транспорт

    private var transportColumn: some View {
        VStack(spacing: 6) {
            Button("Play") { model.play() }
                .buttonStyle(.bordered)
                .controlSize(.large)
                .frame(maxWidth: .infinity)

            Button("Pause") { model.pause() }
                .buttonStyle(.bordered)
                .controlSize(.large)
                .frame(maxWidth: .infinity)

            Button("Stop") { model.stop() }
                .buttonStyle(.bordered)
                .controlSize(.large)
                .frame(maxWidth: .infinity)

            HStack(spacing: 6) {
                Button("previous") { model.previousTrack() }
                    .buttonStyle(.bordered)
                Button("next") { model.nextTrack() }
                    .buttonStyle(.bordered)
            }

            Text(model.statusText)
                .frame(maxWidth: .infinity, alignment: .leading)

            HStack(spacing: 8) {
                Slider(value: Binding(
                    get: { model.volume },
                    set: { model.volume = $0 }
                ), in: 0...1)
                Text(model.volumeText)
                    .monospacedDigit()
                    .frame(width: 90, alignment: .leading)
            }
        }
        .padding(.horizontal, 8)
    }
}