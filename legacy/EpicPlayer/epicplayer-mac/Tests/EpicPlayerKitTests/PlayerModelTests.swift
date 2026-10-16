import EpicCore
import XCTest
@testable import EpicPlayerKit

@MainActor
final class PlayerModelTests: XCTestCase {

    // MARK: - Команды

    func testPlaySendsPlayWithPosition() {
        let h = Harness()
        h.ready()
        h.audio.currentTime = 12.5

        h.model.play()

        XCTAssertEqual(h.model.status, .playing)
        XCTAssertTrue(h.audio.isPlaying)
        XCTAssertEqual(h.transport?.lastBroadcast, FakeTransport.Sent(command: .play, position: 12500, address: nil, port: nil))
    }

    func testPauseSendsPauseWithPosition() {
        let h = Harness()
        h.ready()
        h.model.play()
        h.audio.currentTime = 2.5

        h.model.pause()

        XCTAssertEqual(h.model.status, .paused)
        XCTAssertEqual(h.transport?.lastBroadcast?.command, .pause)
        XCTAssertEqual(h.transport?.lastBroadcast?.position, 2500)
    }

    /// `if (_playbackStatus == Status.stop) return;` в Windows-версии.
    func testPauseIgnoredWhenStopped() {
        let h = Harness()
        h.ready()

        h.model.pause()

        XCTAssertEqual(h.model.status, .stopped)
        XCTAssertTrue(h.transport?.sent.isEmpty ?? false)
    }

    /// Позиция читается после сброса — как `player.Position` после `Stop()`.
    func testStopResetsPositionAndSendsZero() {
        let h = Harness()
        h.ready()
        h.model.play()
        h.audio.currentTime = 42

        h.model.stop()

        XCTAssertEqual(h.model.status, .stopped)
        XCTAssertEqual(h.model.position, 0)
        XCTAssertEqual(h.transport?.lastBroadcast, FakeTransport.Sent(command: .stop, position: 0, address: nil, port: nil))
    }

    func testPreviousAndNextSendZeroAndKeepPlayback() {
        let h = Harness()
        h.ready()
        h.model.play()
        h.audio.currentTime = 10

        h.model.nextTrack()
        XCTAssertEqual(h.transport?.lastBroadcast, FakeTransport.Sent(command: .nextTrack, position: 0, address: nil, port: nil))
        h.model.previousTrack()
        XCTAssertEqual(h.transport?.lastBroadcast, FakeTransport.Sent(command: .previousTrack, position: 0, address: nil, port: nil))

        XCTAssertEqual(h.model.status, .playing)
        XCTAssertTrue(h.audio.isPlaying)
    }

    func testCommandsIgnoredWithoutTrack() {
        let h = Harness()
        h.connect()

        h.model.play()
        h.model.pause()
        h.model.stop()
        h.model.nextTrack()
        h.model.previousTrack()

        XCTAssertEqual(h.model.status, .stopped)
        XCTAssertTrue(h.transport?.sent.isEmpty ?? false)
    }

    func testVolumeAppliedAndSaved() {
        let h = Harness()
        h.model.setVolume(0.8)
        XCTAssertEqual(h.audio.volume, 0.8, accuracy: 0.0001)
        XCTAssertEqual(h.savedSettings.volume, 0.8, accuracy: 0.0001)

        h.model.setVolume(3)
        XCTAssertEqual(h.model.volume, 1)
    }

    // MARK: - Конец трека

    func testTrackEndStopsAndSendsStop() {
        let h = Harness()
        h.ready()
        h.model.play()
        h.audio.currentTime = 300

        h.audio.finish()

        XCTAssertEqual(h.model.status, .stopped)
        XCTAssertEqual(h.model.position, 0)
        XCTAssertEqual(h.transport?.lastBroadcast, FakeTransport.Sent(command: .stop, position: 0, address: nil, port: nil))
    }

    // MARK: - Периодическая отправка

    func testTickWhilePlayingSendsPosition() {
        let h = Harness()
        h.ready()
        h.model.play()
        h.transport?.sent.removeAll()

        h.audio.currentTime = 1.0
        h.model.networkTick()
        h.audio.currentTime = 2.0
        h.model.networkTick()

        XCTAssertEqual(h.transport?.sent.map(\.command), [PlaybackCommand.play, .play])
        XCTAssertEqual(h.transport?.sent.map(\.position), [UInt32(1000), 2000])
    }

    /// Повтор с первой секунды, а не после пятой (app-design.md, 9.1).
    func testTickSendsPositionFromFirstSecond() {
        let h = Harness()
        h.ready()
        h.model.play()
        h.transport?.sent.removeAll()

        h.audio.currentTime = 0.9
        h.model.networkTick()

        XCTAssertEqual(h.transport?.sent.count, 1)
    }

    /// `StopTimerCallback`: stop @ 0 раз в секунду, даже без трека.
    func testTickWhileStoppedRepeatsStop() {
        let h = Harness()
        h.connect()

        h.model.networkTick()
        h.model.networkTick()

        XCTAssertEqual(h.transport?.sent, [
            FakeTransport.Sent(command: .stop, position: 0, address: nil, port: nil),
            FakeTransport.Sent(command: .stop, position: 0, address: nil, port: nil),
        ])
    }

    func testTickWhilePausedSendsNothing() {
        let h = Harness()
        h.ready()
        h.model.play()
        h.model.pause()
        h.transport?.sent.removeAll()

        h.model.networkTick()

        XCTAssertTrue(h.transport?.sent.isEmpty ?? false)
    }

    /// `_suppressPlaybackCommands` на время окна device lookup.
    func testTickSilentWhileLookupOpen() {
        let h = Harness()
        h.ready()
        h.model.openLookup(mode: .udp)
        h.transport?.sent.removeAll()

        h.model.networkTick()
        XCTAssertTrue(h.transport?.sent.isEmpty ?? false)

        h.model.closeLookup()
        h.model.networkTick()
        XCTAssertEqual(h.transport?.sent.count, 1)
    }

    func testRefreshUpdatesPositionOnly() {
        let h = Harness()
        h.ready()
        h.model.play()
        h.transport?.sent.removeAll()
        h.audio.currentTime = 150

        h.model.refresh()

        XCTAssertEqual(h.model.position, 150)
        XCTAssertEqual(h.model.progress, 0.5, accuracy: 0.0001)
        XCTAssertTrue(h.transport?.sent.isEmpty ?? false)
    }

    // MARK: - Ошибки сети

    func testNoInterfaceReportsErrorButPlays() {
        let h = Harness()
        h.model.openTrack(at: URL(fileURLWithPath: "/tmp/show.mp3"))

        h.model.play()

        XCTAssertEqual(h.model.status, .playing)
        XCTAssertNotNil(h.model.networkError)
    }

    func testSendFailureReportedAndClearedOnSuccess() {
        let h = Harness()
        h.ready()
        h.transport?.failSend = true

        h.model.play()
        XCTAssertEqual(h.model.status, .playing, "Воспроизведение не прерывается")
        XCTAssertEqual(h.model.networkError, "sendto failed")

        h.transport?.failSend = false
        h.model.networkTick()
        XCTAssertNil(h.model.networkError)
    }

    func testTransportOpenFailureReported() {
        let h = Harness()
        h.failTransport = true

        h.connect()

        XCTAssertNotNil(h.model.selectedInterface)
        XCTAssertNotNil(h.model.networkError)
    }

    // MARK: - Интерфейс

    func testFirstInterfaceSelectedByDefault() {
        let h = Harness()
        h.connect()
        XCTAssertEqual(h.model.selectedInterface?.name, "en0")
        XCTAssertEqual(h.transport?.interface.name, "en0")
    }

    func testSavedInterfaceRestoredByName() {
        var settings = AppSettings()
        settings.interfaceName = "en7"
        let h = Harness(settings: settings)

        h.connect()

        XCTAssertEqual(h.model.selectedInterface?.name, "en7")
    }

    /// Сохранённый интерфейс временно отсутствует — выбор не перезаписывается.
    func testMissingSavedInterfaceKeepsSetting() {
        var settings = AppSettings()
        settings.interfaceName = "en5"
        let h = Harness(settings: settings)

        h.connect()

        XCTAssertEqual(h.model.selectedInterface?.name, "en0")
        XCTAssertEqual(h.savedSettings.interfaceName, "en5")
    }

    func testSelectingInterfaceReopensTransportAndSaves() {
        let h = Harness()
        h.connect()
        let first = h.transport

        h.model.selectInterface(id: h.interfaces[1].id)

        XCTAssertEqual(first?.isClosed, true)
        XCTAssertEqual(h.transport?.interface.name, "en7")
        XCTAssertEqual(h.savedSettings.interfaceName, "en7")
    }

    func testReloadWithSameListKeepsTransport() {
        let h = Harness()
        h.connect()
        h.connect()
        XCTAssertEqual(h.transports.count, 1)
    }

    // MARK: - Трек

    func testOpenTrackSavesPathAndStopsPrevious() {
        let h = Harness()
        h.ready()
        h.model.play()

        h.model.openTrack(at: URL(fileURLWithPath: "/tmp/second.wav"))

        XCTAssertEqual(h.model.status, .stopped)
        XCTAssertEqual(h.model.trackTitle, "second.wav")
        XCTAssertEqual(h.model.duration, 300)
        XCTAssertEqual(h.savedSettings.audioFilePath, "/tmp/second.wav")
    }

    func testOpenTrackFailure() {
        let h = Harness()
        h.audio.failLoad = true

        h.model.openTrack(at: URL(fileURLWithPath: "/tmp/broken.mp3"))

        XCTAssertFalse(h.model.hasTrack)
        XCTAssertNotNil(h.model.trackError)
        XCTAssertNil(h.savedSettings.audioFilePath)
    }

    func testMissingSavedTrackShowsNotFound() async {
        var settings = AppSettings()
        settings.audioFilePath = "/nonexistent/\(UUID().uuidString).mp3"
        let h = Harness(settings: settings)

        await h.model.start()

        XCTAssertFalse(h.model.hasTrack)
        XCTAssertEqual(h.model.trackError, "File not found")
        XCTAssertEqual(h.savedSettings.audioFilePath, settings.audioFilePath, "Путь не стирается")
    }

    // MARK: - Art-Net

    func testEnablingArtNetOpensSourceWithChannelIndex() {
        let h = Harness()
        h.connect()
        var settings = ArtNetSettings()
        settings.enabled = true
        settings.channel = 1

        h.model.updateArtNet(settings)

        XCTAssertTrue(h.model.isArtNetRunning)
        XCTAssertNil(h.artNets.last?.interface, "По умолчанию — любой интерфейс")
        XCTAssertEqual(h.artNets.last?.settings.channel, 0, "Канал 1 — индекс 0")
        XCTAssertTrue(h.savedSettings.artNet.enabled)
    }

    func testArtNetValueChangeUpdatesWithoutReopen() {
        let h = Harness()
        h.connect()
        var settings = ArtNetSettings()
        settings.enabled = true
        h.model.updateArtNet(settings)

        settings.playValue = 60
        h.model.updateArtNet(settings)

        XCTAssertEqual(h.artNets.count, 1)
        XCTAssertEqual(h.artNets.last?.settings.playValue, 60)
    }

    func testArtNetInterfaceChangeReopens() {
        let h = Harness()
        h.connect()
        var settings = ArtNetSettings()
        settings.enabled = true
        h.model.updateArtNet(settings)

        settings.interfaceName = "en7"
        h.model.updateArtNet(settings)

        XCTAssertEqual(h.artNets.count, 2)
        XCTAssertEqual(h.artNets.first?.isClosed, true)
        XCTAssertEqual(h.artNets.last?.interface?.name, "en7")
    }

    func testArtNetUnavailableInterfaceReported() {
        let h = Harness()
        h.connect()
        var settings = ArtNetSettings()
        settings.enabled = true
        settings.interfaceName = "en9"

        h.model.updateArtNet(settings)

        XCTAssertFalse(h.model.isArtNetRunning)
        XCTAssertNotNil(h.model.artNetError)
    }

    func testArtNetOpenFailureTurnsToggleOff() {
        let h = Harness()
        h.connect()
        h.failArtNet = true
        var settings = ArtNetSettings()
        settings.enabled = true

        h.model.updateArtNet(settings)

        XCTAssertFalse(h.model.artNet.enabled)
        XCTAssertFalse(h.savedSettings.artNet.enabled)
        XCTAssertNotNil(h.model.artNetError)
    }

    func testDisablingArtNetClosesSource() {
        let h = Harness()
        h.connect()
        var settings = ArtNetSettings()
        settings.enabled = true
        h.model.updateArtNet(settings)

        settings.enabled = false
        h.model.updateArtNet(settings)

        XCTAssertEqual(h.artNets.last?.isClosed, true)
        XCTAssertFalse(h.model.isArtNetRunning)
    }

    func testDmxCommandsDriveTransport() {
        let h = Harness()
        h.ready()
        var settings = ArtNetSettings()
        settings.enabled = true
        h.model.updateArtNet(settings)

        h.artNets.last?.onCommand?(.play)
        XCTAssertEqual(h.model.status, .playing)
        XCTAssertEqual(h.transport?.lastBroadcast?.command, .play)

        h.artNets.last?.onCommand?(.pause)
        XCTAssertEqual(h.model.status, .paused)

        h.artNets.last?.onCommand?(.stop)
        XCTAssertEqual(h.model.status, .stopped)
    }

    /// `player.HasAudio` в Windows-версии.
    func testDmxIgnoredWithoutTrack() {
        let h = Harness()
        h.connect()
        var settings = ArtNetSettings()
        settings.enabled = true
        h.model.updateArtNet(settings)

        h.artNets.last?.onCommand?(.play)

        XCTAssertEqual(h.model.status, .stopped)
        XCTAssertTrue(h.transport?.sent.isEmpty ?? false)
    }

    func testArtNetStartsOnLaunchWhenEnabled() async {
        var settings = AppSettings()
        settings.artNet.enabled = true
        let h = Harness(settings: settings)

        await h.model.start()

        XCTAssertEqual(h.artNets.count, 1)
        XCTAssertTrue(h.model.isArtNetRunning)
    }

    // MARK: - Выход

    func testShutdownClosesEverything() {
        let h = Harness()
        h.connect()
        var settings = ArtNetSettings()
        settings.enabled = true
        h.model.updateArtNet(settings)
        h.model.openLookup(mode: .tcp)

        h.model.shutdown()

        XCTAssertEqual(h.transport?.isClosed, true)
        XCTAssertEqual(h.artNets.last?.isClosed, true)
        XCTAssertEqual(h.listeners.last?.isClosed, true)
        XCTAssertNil(h.model.lookup)
    }
}
