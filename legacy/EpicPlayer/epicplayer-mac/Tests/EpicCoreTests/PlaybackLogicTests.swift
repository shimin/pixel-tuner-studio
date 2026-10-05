import XCTest
@testable import EpicCore

/// Логика воспроизведения: состояние, таймер позиции, расчёты для UI.
///
/// Настоящее воспроизведение аудио проверяется отдельно, здесь —
/// всё, что можно проверить без звуковой карты: перевод времени,
/// прогресс, и главное — периодическая отправка позиции раз в секунду,
/// как решено в plans/protocol.md.
final class PlaybackLogicTests: XCTestCase {

    // MARK: - Состояние воспроизведения

    /// Подписи состояний перенесены из Windows-версии: интерфейс должен
    /// выглядеть так же, см. `MainWindow.xaml.cs:88-96`.
    func testStatusDescriptions() {
        XCTAssertEqual(PlaybackStatus.play.description, "Status: Play")
        XCTAssertEqual(PlaybackStatus.pause.description, "Status: Pause")
        XCTAssertEqual(PlaybackStatus.stop.description, "Status: Stop")
    }

    /// Начальное состояние — stop, как в Windows-версии.
    func testInitialStatusIsStop() {
        let controller = PlaybackController()
        XCTAssertEqual(controller.status, .stop)
        XCTAssertFalse(controller.isPlaying)
    }

    // MARK: - Формат времени

    /// Время выводится как mm:ss, минуты не ограничены 59.
    /// Windows-код: `TimeSpan.FromSeconds(...).ToString(@"mm\:ss")`.
    func testTimeFormatting() {
        XCTAssertEqual(PlaybackFormatting.time(seconds: 0), "00:00")
        XCTAssertEqual(PlaybackFormatting.time(seconds: 5), "00:05")
        XCTAssertEqual(PlaybackFormatting.time(seconds: 65), "01:05")
        XCTAssertEqual(PlaybackFormatting.time(seconds: 599), "09:59")
        XCTAssertEqual(PlaybackFormatting.time(seconds: 600), "10:00")
    }

    /// Трек длиннее часа: минуты продолжаются за 59.
    /// Двузначный формат mm не обрезает число до двух цифр.
    func testTimeFormattingBeyondOneHour() {
        XCTAssertEqual(PlaybackFormatting.time(seconds: 3600), "60:00")
        XCTAssertEqual(PlaybackFormatting.time(seconds: 3661), "61:01")
        XCTAssertEqual(PlaybackFormatting.time(seconds: 7200), "120:00")
    }

    /// Дробные секунды округляются вниз, как `TimeSpan`.
    func testTimeFormattingTruncatesFractions() {
        XCTAssertEqual(PlaybackFormatting.time(seconds: 59.9), "00:59")
        XCTAssertEqual(PlaybackFormatting.time(seconds: -1), "00:00")
    }

    // MARK: - Прогресс

    /// Полоса прогресса — процент прослушанного.
    /// Windows-код: `position / duration * 100`.
    func testProgressPercentage() {
        XCTAssertEqual(PlaybackFormatting.progress(position: 0, duration: 100), 0)
        XCTAssertEqual(PlaybackFormatting.progress(position: 25, duration: 100), 25)
        XCTAssertEqual(PlaybackFormatting.progress(position: 100, duration: 100), 100)
    }

    /// Нулевая или неизвестная длительность не должна давать деление
    /// на ноль — Windows-код полагался на `try/catch`.
    func testProgressWithZeroDurationIsSafe() {
        XCTAssertEqual(PlaybackFormatting.progress(position: 10, duration: 0), 0)
        XCTAssertEqual(PlaybackFormatting.progress(position: 10, duration: nil), 0)
    }

    /// Громкость показывается целым числом процентов.
    /// Windows-код: `Math.Round(value * 100, 0)`.
    func testVolumeLabel() {
        XCTAssertEqual(PlaybackFormatting.volumeLabel(0.5), "Volume: 50%")
        XCTAssertEqual(PlaybackFormatting.volumeLabel(0), "Volume: 0%")
        XCTAssertEqual(PlaybackFormatting.volumeLabel(1), "Volume: 100%")
        XCTAssertEqual(PlaybackFormatting.volumeLabel(0.256), "Volume: 26%")
    }

    // MARK: - Команды воспроизведения

    /// Каждая команда отправляется один раз при нажатии.
    func testCommandsAreSentOnce() {
        let recorder = CommandRecorder()
        let controller = PlaybackController()
        controller.onCommand = recorder.record
        controller.setInterface(makeInterface())

        controller.send(.play)
        XCTAssertEqual(recorder.count, 1)
        XCTAssertEqual(recorder.lastCommand, .play)

        controller.send(.stop)
        XCTAssertEqual(recorder.count, 2)
        XCTAssertEqual(recorder.lastCommand, .stop)
    }

    /// Пустые команды не отправляются: без выбранного интерфейса
    /// отправка упала бы с ошибкой сокета.
    func testCommandsAreNotSentWhenSuppressed() {
        let recorder = CommandRecorder()
        let controller = PlaybackController()
        controller.onCommand = recorder.record

        // Интерфейс не выбран.
        controller.setInterface(nil)
        controller.send(.play)
        XCTAssertEqual(recorder.count, 0)
    }

    // MARK: - Периодическая отправка позиции

    /// Позиция уходит раз в секунду во время воспроизведения —
    /// решение владельца, зафиксированное в plans/protocol.md.
    func testPositionSentOncePerSecond() {
        let recorder = CommandRecorder()
        let controller = PlaybackController()
        controller.onCommand = recorder.record
        controller.setInterface(makeInterface())
        controller.status = .play

        // Первая отправка — сразу при старте.
        controller.tick(positionMilliseconds: 0)
        XCTAssertEqual(recorder.count, 1)
        XCTAssertEqual(recorder.lastPosition, 0)

        // В течение секунды тиков больше нет.
        controller.tick(positionMilliseconds: 400)
        controller.tick(positionMilliseconds: 900)
        XCTAssertEqual(recorder.count, 1)

        // Прошла секунда — отправка.
        controller.tick(positionMilliseconds: 1000)
        XCTAssertEqual(recorder.count, 2)
        XCTAssertEqual(recorder.lastPosition, 1000)
    }

    /// Без интерфейса позиция не отправляется, но счётчик не сбрасывается:
    /// после выбора интерфейса отправка продолжится.
    func testPositionNotSentWithoutInterface() {
        let recorder = CommandRecorder()
        let controller = PlaybackController()
        controller.onCommand = recorder.record
        controller.status = .play

        controller.tick(positionMilliseconds: 0)
        controller.tick(positionMilliseconds: 1000)
        XCTAssertEqual(recorder.count, 0)
    }

    /// На паузе и после остановки позиция не отправляется.
    func testPositionOnlySentWhilePlaying() {
        let recorder = CommandRecorder()
        let controller = PlaybackController()
        controller.onCommand = recorder.record
        controller.setInterface(makeInterface())

        controller.status = .pause
        controller.tick(positionMilliseconds: 1000)
        XCTAssertEqual(recorder.count, 0)

        controller.status = .stop
        controller.tick(positionMilliseconds: 2000)
        XCTAssertEqual(recorder.count, 0)
    }

    /// Отправка возобновляется после паузы: счётчик сбрасывается,
    /// чтобы первая же позиция ушла сразу.
    func testPositionResumesAfterPause() {
        let recorder = CommandRecorder()
        let controller = PlaybackController()
        controller.onCommand = recorder.record
        controller.setInterface(makeInterface())

        controller.status = .play
        controller.tick(positionMilliseconds: 1000)
        XCTAssertEqual(recorder.count, 1)

        // Пауза, затем продолжение с того же места.
        controller.status = .pause
        controller.tick(positionMilliseconds: 5000)

        controller.status = .play
        controller.resetPositionClock()
        controller.tick(positionMilliseconds: 5000)
        XCTAssertEqual(recorder.count, 2)
        XCTAssertEqual(recorder.lastPosition, 5000)
    }

    /// Перемотка назад: счётчик не переполняется, отправка возобновляется
    /// только когда позиция снова прошла секунду от последней отправки.
    func testSeekBackwardsDoesNotOverflow() {
        let recorder = CommandRecorder()
        let controller = PlaybackController()
        controller.onCommand = recorder.record
        controller.setInterface(makeInterface())
        controller.status = .play

        controller.tick(positionMilliseconds: 30_000)
        XCTAssertEqual(recorder.count, 1)

        // Перемотка назад: переполнения быть не должно.
        controller.tick(positionMilliseconds: 5_000)
        XCTAssertEqual(recorder.count, 1, "перемотка назад не должна слать позицию")

        // И снова вперёд, но секунда ещё не прошла с момента отправки.
        controller.tick(positionMilliseconds: 6_000)
        XCTAssertEqual(recorder.count, 1)

        // Секунда прошла — отправка.
        controller.tick(positionMilliseconds: 31_000)
        XCTAssertEqual(recorder.count, 2)
        XCTAssertEqual(recorder.lastPosition, 31_000)
    }

    /// Позиция уходит с кодом play — устройство использует её для
    /// синхронизации, а не только команду паузы.
    func testPositionUsesPlayCommand() {
        let recorder = CommandRecorder()
        let controller = PlaybackController()
        controller.onCommand = recorder.record
        controller.setInterface(makeInterface())
        controller.status = .play

        controller.tick(positionMilliseconds: 1000)
        XCTAssertEqual(recorder.lastCommand, .play)
    }

    /// Подтверждённое решение: интервал 1 секунда, как в Windows-версии,
    /// хотя README обещал 5 секунд. Расхождение задокументировано
    /// в plans/protocol.md.
    func testPositionIntervalIsOneSecond() {
        XCTAssertEqual(PlaybackController.positionIntervalMilliseconds, 1000)
    }
}

/// Интерфейс в сети шоу: без него отправка невозможна.
    private func makeInterface() -> NetworkInterface {
        NetworkInterface(
            name: "en0", index: 4,
            addresses: [IPv4Address([192, 168, 88, 10])],
            netmask: SubnetMask([255, 255, 255, 0]),
            kind: .ethernet)
    }

    /// Записывает отправленные команды.
final class CommandRecorder {
    private(set) var commands: [(command: PlaybackCommand, position: UInt32)] = []

    var count: Int { commands.count }
    var lastCommand: PlaybackCommand? { commands.last?.command }
    var lastPosition: UInt32? { commands.last?.position }

    func record(_ command: PlaybackCommand, positionMilliseconds: UInt32) {
        commands.append((command, positionMilliseconds))
    }
}