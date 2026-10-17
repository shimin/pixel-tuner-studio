import EpicCore
import EpicPlayerKit
import SwiftUI
import UniformTypeIdentifiers

/// Главное окно по `MainWindow.xaml`: трек и сеть слева, транспорт
/// в центре, Art-Net справа. См. plans/app-design.md, раздел 10.
struct MainView: View {
    let model: PlayerModel

    @State private var isImporting = false
    /// Панель Art-Net при запуске свёрнута: раскрытая отдала бы фокус полю
    /// Channel, и случайное нажатие клавиши поменяло бы канал DMX.
    @State private var showArtNet = false

    var body: some View {
        HStack(alignment: .top, spacing: 12) {
            trackColumn
                .frame(minWidth: 280, maxWidth: .infinity)
            transportColumn
                .frame(width: 200)
            artNetColumn
        }
        .padding(8)
        .frame(minWidth: 600, minHeight: 320, alignment: .topLeading)
        .background(Theme.background)
        .preferredColorScheme(.dark)
        .fileImporter(isPresented: $isImporting, allowedContentTypes: [.audio]) { result in
            if case .success(let url) = result {
                model.openTrack(at: url)
            }
        }
        .sheet(item: lookupBinding) { lookup in
            DeviceLookupView(model: lookup, onClose: { model.closeLookup() })
        }
    }

    // MARK: - Трек и сеть

    private var trackColumn: some View {
        VStack(alignment: .leading, spacing: 8) {
            Button { isImporting = true } label: {
                Text("Open audio").frame(maxWidth: .infinity, minHeight: 24)
            }
            .controlSize(.large)

            HStack {
                Picker("Interface", selection: interfaceBinding) {
                    Text("No interface").tag(String?.none)
                    ForEach(model.interfaces) { interface in
                        Text(interface.displayName).tag(Optional(interface.id))
                    }
                }
                .labelsHidden()
                .help("Network interface to communicate")

                Button {
                    Task { await model.reloadInterfaces() }
                } label: {
                    Image(systemName: "arrow.clockwise")
                }
                .help("Refresh interface list")
            }

            Button { model.openLookup(mode: .udp) } label: {
                Text("Device lookup").frame(maxWidth: .infinity)
            }
            Button { model.openLookup(mode: .tcp) } label: {
                Text("TCP device lookup").frame(maxWidth: .infinity)
            }

            Text(model.trackTitle ?? "No file")
                .lineLimit(1)
                .truncationMode(.middle)
                .padding(.top, 4)
            if let trackError = model.trackError {
                Text(trackError).foregroundStyle(Theme.error)
            }

            ProgressView(value: model.progress)
                .tint(Theme.progress)

            HStack {
                Text(TimeFormat.clock(model.position))
                Spacer()
                Text(TimeFormat.clock(model.duration))
            }
            .monospacedDigit()

            if let networkError = model.networkError {
                Text(networkError)
                    .font(.caption)
                    .foregroundStyle(Theme.error)
                    .lineLimit(2)
                    .help(networkError)
            }
        }
    }

    // MARK: - Транспорт

    private var transportColumn: some View {
        VStack(spacing: 8) {
            if !model.hasTrack {
                Text("No media available").foregroundStyle(Theme.error)
            }

            Group {
                TransportButton(title: "Play") { model.play() }
                TransportButton(title: "Pause") { model.pause() }
                TransportButton(title: "Stop") { model.stop() }
                HStack {
                    TransportButton(title: "previous") { model.previousTrack() }
                        .help("previous image")
                    TransportButton(title: "next") { model.nextTrack() }
                        .help("next image")
                }
            }
            .disabled(!model.hasTrack)

            Text(model.status.title)
                .frame(maxWidth: .infinity, alignment: .leading)

            Slider(value: volumeBinding, in: 0...1)
            Text(verbatim: "Volume: \(Int((model.volume * 100).rounded()))%")
                .frame(maxWidth: .infinity, alignment: .leading)
        }
    }

    // MARK: - Art-Net

    @ViewBuilder
    private var artNetColumn: some View {
        if showArtNet {
            VStack(alignment: .leading) {
                Button("Art-Net settings ▸") { showArtNet = false }
                    .buttonStyle(.plain)
                ArtNetSettingsView(model: model)
            }
            .frame(width: 250)
            .padding(8)
            .background(Theme.panel, in: RoundedRectangle(cornerRadius: 6))
        } else {
            Button { showArtNet = true } label: {
                Text("◂ Art-Net settings")
                    .foregroundStyle(artNetTabColor)
                    .fixedSize()
                    .rotationEffect(.degrees(-90))
                    .frame(width: 24, height: 140)
            }
            .buttonStyle(.plain)
            .help(artNetTabHelp)
        }
    }

    /// Состояние Art-Net видно и на свёрнутой панели: зелёный — слушает,
    /// красный — ошибка (порт занят, интерфейса нет).
    private var artNetTabColor: Color {
        if model.artNetError != nil { return Theme.error }
        return model.isArtNetRunning ? Theme.progress : .primary
    }

    private var artNetTabHelp: String {
        if let error = model.artNetError { return error }
        return model.isArtNetRunning ? "Art-Net: listening on UDP 6454" : "Art-Net: off"
    }

    // MARK: - Привязки

    private var interfaceBinding: Binding<String?> {
        Binding(
            get: { model.selectedInterface?.id },
            set: { model.selectInterface(id: $0) }
        )
    }

    private var volumeBinding: Binding<Double> {
        Binding(get: { model.volume }, set: { model.setVolume($0) })
    }

    /// Закрытие листа (Esc, Close) — `closeLookup()`, который включает
    /// обратно периодическую отправку.
    private var lookupBinding: Binding<DeviceLookupModel?> {
        Binding(
            get: { model.lookup },
            set: { if $0 == nil { model.closeLookup() } }
        )
    }
}
