import AppKit
import EpicPlayerKit
import SwiftUI

@main
struct EpicPlayerApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    @State private var model = PlayerModel.live()

    var body: some Scene {
        // Одно окно, как в Windows-версии; закрытие окна завершает приложение.
        Window("E-pic player", id: "main") {
            MainView(model: model)
                .task {
                    appDelegate.attach(model)
                    await model.start()
                }
        }
        .defaultSize(width: 800, height: 340)
        .windowResizability(.contentMinSize)
    }
}

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    private var model: PlayerModel?
    private let clock = PlaybackClock()

    func attach(_ model: PlayerModel) {
        guard self.model == nil else { return }
        self.model = model
        clock.start(model)
    }

    func applicationDidFinishLaunching(_ notification: Notification) {
        // Нужно при запуске через `swift run`, без бандла: иначе окно
        // не получает фокус и нет иконки в Dock.
        NSApp.setActivationPolicy(.regular)
        NSApp.activate()
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        true
    }

    func applicationWillTerminate(_ notification: Notification) {
        clock.stop()
        model?.shutdown()
    }
}
