import XCTest
@testable import EpicCore

/// Настройки сохраняются и восстанавливаются между запусками.
///
/// Windows-версия использовала `Settings.Default`
/// (`ApplicationSettingsBase`), здесь — `UserDefaults`.
final class AppSettingsTests: XCTestCase {
    private var defaults: UserDefaults!
    private var suiteName: String!

    override func setUp() {
        super.setUp()
        suiteName = "EpicPlayerTests.\(UUID().uuidString)"
        defaults = UserDefaults(suiteName: suiteName)
    }

    override func tearDown() {
        defaults.removePersistentDomain(forName: suiteName)
        defaults = nil
        super.tearDown()
    }

    /// Значения по умолчанию — как в настройках Windows-версии.
    func testDefaultsMatchWindowsVersion() {
        let settings = AppSettings(defaults: defaults)

        XCTAssertEqual(settings.filePath, "")
        XCTAssertEqual(settings.interfaceIndex, 0)
        XCTAssertEqual(settings.wifiPort, 333)

        let artNet = settings.artNet
        XCTAssertEqual(artNet.localIP, "192.168.88.253")
        XCTAssertEqual(artNet.subnetMask, "255.255.255.0")
        XCTAssertEqual(artNet.port, "3333")
        XCTAssertEqual(artNet.universe, "1")
        XCTAssertFalse(artNet.isEnabled)
    }

    /// Пороги Art-Net: 50 / 100 / 150 — start, pause, stop.
    ///
    /// В `MainWindow.xaml` указано наоборот (stop 100, pause 150), но эти
    /// значения перезаписываются из настроек и пользователю не видны.
    func testArtNetThresholdsMatchSettings() {
        let settings = AppSettings(defaults: defaults)
        let thresholds = settings.artNet.thresholds

        XCTAssertEqual(thresholds.start, 50)
        XCTAssertEqual(thresholds.pause, 100)
        XCTAssertEqual(thresholds.stop, 150)
    }

    /// Настройки переживают перезапуск: новый экземпляр читает то же.
    func testSettingsPersist() {
        let first = AppSettings(defaults: defaults)
        first.filePath = "/Users/tech/track.mp3"
        first.interfaceIndex = 2
        first.wifiPort = 444
        first.artNet = ArtNetSettings(
            localIP: "10.0.5.20",
            subnetMask: "255.255.0.0",
            port: "6454",
            universe: "3",
            channel: 7,
            start: 10,
            pause: 20,
            stop: 30,
            isEnabled: true)

        let second = AppSettings(defaults: defaults)
        XCTAssertEqual(second.filePath, "/Users/tech/track.mp3")
        XCTAssertEqual(second.interfaceIndex, 2)
        XCTAssertEqual(second.wifiPort, 444)

        let artNet = second.artNet
        XCTAssertEqual(artNet.localIP, "10.0.5.20")
        XCTAssertEqual(artNet.subnetMask, "255.255.0.0")
        XCTAssertEqual(artNet.port, "6454")
        XCTAssertEqual(artNet.universe, "3")
        XCTAssertEqual(artNet.channel, 7)
        XCTAssertEqual(artNet.start, 10)
        XCTAssertEqual(artNet.pause, 20)
        XCTAssertEqual(artNet.stop, 30)
        XCTAssertTrue(artNet.isEnabled)
    }

    /// Некорректный порт в настройках не должен ломать работу:
    /// используется значение по умолчанию.
    func testInvalidPortFallsBackToDefault() {
        defaults.set(0, forKey: "WifiPort")
        XCTAssertEqual(AppSettings(defaults: defaults).wifiPort, 333)

        defaults.set(-1, forKey: "WifiPort")
        XCTAssertEqual(AppSettings(defaults: defaults).wifiPort, 333)

        defaults.set(70000, forKey: "WifiPort")
        XCTAssertEqual(AppSettings(defaults: defaults).wifiPort, 333)
    }

    /// Путь к несуществующему файлу должен возвращаться как nil,
    /// а не как пустая строка: различать «не выбран» и «выбран и удалён».
    func testExistingFileDetection() {
        let settings = AppSettings(defaults: defaults)

        XCTAssertNil(settings.existingFileURL)

        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("epic-settings-\(UUID().uuidString).mp3")
        try? Data("x".utf8).write(to: url)
        defer { try? FileManager.default.removeItem(at: url) }

        settings.filePath = url.path
        XCTAssertEqual(settings.existingFileURL, url)
    }
}