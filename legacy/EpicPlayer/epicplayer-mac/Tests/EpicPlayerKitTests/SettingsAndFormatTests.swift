import EpicCore
import XCTest
@testable import EpicPlayerKit

@MainActor
final class SettingsAndFormatTests: XCTestCase {

    private func makeDefaults() -> UserDefaults {
        let suite = "EpicPlayerKitTests.\(UUID().uuidString)"
        let defaults = UserDefaults(suiteName: suite)!
        defaults.removePersistentDomain(forName: suite)
        return defaults
    }

    // MARK: - Настройки

    /// Значения по умолчанию — из Settings.settings Windows-версии.
    func testDefaultsMatchWindows() {
        let settings = SettingsStore(defaults: makeDefaults(), debounce: 0).load()
        XCTAssertNil(settings.audioFilePath)
        XCTAssertEqual(settings.volume, 0.5)
        XCTAssertFalse(settings.artNet.enabled)
        XCTAssertNil(settings.artNet.universe)
        XCTAssertEqual(settings.artNet.channel, 1)
        XCTAssertEqual(settings.artNet.playValue, 50)
        XCTAssertEqual(settings.artNet.pauseValue, 100)
        XCTAssertEqual(settings.artNet.stopValue, 150)
    }

    func testRoundTrip() {
        let defaults = makeDefaults()
        var settings = AppSettings()
        settings.audioFilePath = "/Users/show/track.mp3"
        settings.interfaceName = "en7"
        settings.volume = 0.3
        settings.artNet.enabled = true
        settings.artNet.universe = 2
        settings.artNet.channel = 12

        SettingsStore(defaults: defaults, debounce: 0).save(settings)

        XCTAssertEqual(SettingsStore(defaults: defaults, debounce: 0).load(), settings)
    }

    func testCorruptDataGivesDefaults() {
        let defaults = makeDefaults()
        defaults.set(Data("not json".utf8), forKey: SettingsStore.key)
        XCTAssertEqual(SettingsStore(defaults: defaults, debounce: 0).load(), AppSettings())
    }

    /// Старая версия не знала поля — остальное читается.
    func testMissingKeysUseDefaults() {
        let defaults = makeDefaults()
        defaults.set(Data(#"{"volume":0.2,"artNet":{"enabled":true}}"#.utf8), forKey: SettingsStore.key)

        let settings = SettingsStore(defaults: defaults, debounce: 0).load()

        XCTAssertEqual(settings.volume, 0.2)
        XCTAssertTrue(settings.artNet.enabled)
        XCTAssertEqual(settings.artNet.playValue, 50)
    }

    func testDebouncedSaveWrittenOnFlush() {
        let defaults = makeDefaults()
        let store = SettingsStore(defaults: defaults, debounce: 60)
        var settings = AppSettings()
        settings.volume = 0.9

        store.save(settings)
        XCTAssertNil(defaults.data(forKey: SettingsStore.key), "До flush не записано")

        store.flush()
        XCTAssertEqual(store.load().volume, 0.9)
    }

    func testTriggerSettingsConversion() {
        var settings = ArtNetSettings()
        settings.channel = 512
        settings.playValue = 300
        settings.stopValue = -5
        settings.universe = 70000

        let trigger = settings.triggerSettings

        XCTAssertEqual(trigger.channel, 511)
        XCTAssertEqual(trigger.playValue, 255)
        XCTAssertEqual(trigger.stopValue, 0)
        XCTAssertEqual(trigger.universe, 32767)

        settings.channel = 0
        XCTAssertEqual(settings.triggerSettings.channel, 0, "Канал меньше 1 — первый")
    }

    // MARK: - Время

    func testClockFormat() {
        XCTAssertEqual(TimeFormat.clock(0), "00:00")
        XCTAssertEqual(TimeFormat.clock(59.99), "00:59")
        XCTAssertEqual(TimeFormat.clock(296), "04:56")
        XCTAssertEqual(TimeFormat.clock(3599), "59:59")
        XCTAssertEqual(TimeFormat.clock(3600), "1:00:00")
        XCTAssertEqual(TimeFormat.clock(.nan), "00:00")
        XCTAssertEqual(TimeFormat.clock(-3), "00:00")
    }

    func testMilliseconds() {
        XCTAssertEqual(TimeFormat.milliseconds(1.2345), 1234)
        XCTAssertEqual(TimeFormat.milliseconds(-1), 0)
        XCTAssertEqual(TimeFormat.milliseconds(.infinity), 0)
        XCTAssertEqual(TimeFormat.milliseconds(5_000_000), UInt32.max)
    }
}
