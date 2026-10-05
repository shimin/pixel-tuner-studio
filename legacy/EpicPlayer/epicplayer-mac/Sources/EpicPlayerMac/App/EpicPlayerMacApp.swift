import SwiftUI

/// Точка входа приложения.
///
/// Фаза 0: проверяется, что приложение собирается и запускается.
/// Интерфейс появится в фазе 4, сетевое подключение — в фазе 2.
@main
struct EpicPlayerMacApp: App {
    var body: some Scene {
        WindowGroup {
            ContentView()
                .frame(minWidth: 600, minHeight: 320)
        }
        .windowResizability(.contentMinSize)
        .commands {
            // Без меню приложение на macOS открывается с непривычным
            // набором пунктов: нет Quit, Hide и Copy/Paste.
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
            if let version = Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String {
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

/// Заглушка главного окна.
///
/// Компоновка повторит `MainWindow.xaml`: выбор интерфейса, кнопки
/// device lookup, транспорт и громкость. Здесь только каркас, чтобы
/// убедиться, что сборка и запуск работают.
struct ContentView: View {
    var body: some View {
        VStack(spacing: 12) {
            Text("E-pic player")
                .font(.title2.bold())

            Text("Фаза 0: каркас приложения")
                .foregroundStyle(.secondary)

            Text("Протокол и сетевой слой готовы и покрыты тестами.")
                .font(.callout)
                .foregroundStyle(.secondary)
        }
        .padding(32)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}