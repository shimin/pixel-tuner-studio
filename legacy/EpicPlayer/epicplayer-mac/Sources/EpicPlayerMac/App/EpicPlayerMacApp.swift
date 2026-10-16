import SwiftUI

/// Точка входа приложения.
@main
struct EpicPlayerMacApp: App {
    var body: some Scene {
        WindowGroup {
            ContentView()
        }
        .windowResizability(.contentMinSize)
        .commands {
            // Без этого приложение на macOS открывается с непривычным
            // набором пунктов меню: нет Quit, Hide и Edit.
            CommandGroup(replacing: .appInfo) {
                AboutWindowView()
            }
            CommandGroup(replacing: .newItem) {}
        }
    }
}

/// Окно «О приложении» с версией из бандла.
private struct AboutWindowView: View {
    var body: some View {
        VStack(spacing: 12) {
            Image(systemName: "music.note.list")
                .font(.system(size: 48))
            Text("E-pic player")
                .font(.title2.bold())
            if let version = Bundle.main.infoDictionary?["CFBundleShortVersionString"]
                as? String {
                Text("Версия \(version)")
                    .foregroundStyle(.secondary)
            }
            Text("Управление шоу с компьютера")
                .font(.callout)
                .foregroundStyle(.secondary)
        }
        .padding(24)
        .frame(width: 320)
    }
}