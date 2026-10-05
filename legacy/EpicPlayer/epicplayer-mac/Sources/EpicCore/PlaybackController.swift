import Foundation

/// Состояние воспроизведения.
///
/// Подписи перенесены из Windows-версии, см. `MainWindow.xaml.cs:88-96`,
/// чтобы интерфейс выглядел так же.
public enum PlaybackStatus: String, CaseIterable, Sendable {
    case play
    case pause
    case stop

    public var description: String {
        switch self {
        case .play: return "Status: Play"
        case .pause: return "Status: Pause"
        case .stop: return "Status: Stop"
        }
    }
}

/// Форматирование значений для интерфейса.
///
/// Повторяет вычисления Windows-версии, чтобы внешний вид и значения
/// совпадали с привычными.
public enum PlaybackFormatting {
    /// Время как `mm:ss`, минуты не ограничены 59.
    ///
    /// Windows-код: `TimeSpan.FromSeconds(...).ToString(@"mm\:ss")`.
    public static func time(seconds: Double) -> String {
        let total = Int(seconds.rounded(.down))
        let safeTotal = max(0, total)
        let minutes = safeTotal / 60
        let secondsPart = safeTotal % 60
        return String(format: "%02d:%02d", minutes, secondsPart)
    }

    /// Процент прослушанного для полосы прогресса.
    ///
    /// Windows-код делил на длительность в `try/catch`; здесь нулевая
    /// длительность обрабатывается явно.
    public static func progress(position: Double, duration: Double?) -> Double {
        guard let duration = duration, duration > 0 else { return 0 }
        let percent = position / duration * 100
        // Полоса прогресса не должна уходить за пределы.
        return min(max(percent, 0), 100)
    }

    /// Подпись громкости.
    ///
    /// Windows-код: `Math.Round(value * 100, 0)`.
    public static func volumeLabel(_ value: Double) -> String {
        let percent = Int((value * 100).rounded())
        return "Volume: \(percent)%"
    }
}

/// Логика воспроизведения без привязки к аудио.
///
/// Разделение намеренное: здесь всё, что можно проверить тестом —
/// состояние, периодическая отправка позиции, перемотка. Сам проигрыватель
/// подключается снаружи через `PlaybackControlling`.
public final class PlaybackController {
    /// Интервал отправки позиции на устройство.
    ///
    /// Раз в секунду — совпадает с фактическим поведением Windows-версии,
    /// хотя её README обещает 5 секунд. Расхождение задокументировано
    /// в `plans/protocol.md`.
    public static let positionIntervalMilliseconds: UInt32 = 1000

    /// Выбранный интерфейс. Без него отправка невозможна.
    public private(set) var interface: NetworkInterface?

    /// Текущее состояние.
    public var status: PlaybackStatus = .stop {
        didSet {
            if status != .play { lastPositionSent = nil }
        }
    }

    public var isPlaying: Bool { status == .play }

    /// Вызывается для каждой команды, готовой к отправке.
    public var onCommand: ((PlaybackCommand, UInt32) -> Void)?

    /// Позиция, отправленная в прошлый раз. `nil` — ещё не отправлялась.
    private var lastPositionSent: UInt32?

    public init() {}

    /// Задать интерфейс. Без него команды не отправляются.
    public func setInterface(_ interface: NetworkInterface?) {
        self.interface = interface
    }

    /// Отправить команду с текущей позицией.
    public func send(_ command: PlaybackCommand, positionMilliseconds: UInt32 = 0) {
        guard interface != nil else { return }
        onCommand?(command, positionMilliseconds)
    }

    /// Тик таймера позиции: раз в секунду во время воспроизведения.
    ///
    /// Windows-код отправлял позицию каждую секунду после первых пяти:
    /// счётчик `_lastMsSent` не обновлялся после отправки, поэтому
    /// условие `timepoint - 0 > 5000` оставалось истинным. Здесь отправка
    /// привязана к интервалу явно, как и решено в `plans/protocol.md`.
    public func tick(positionMilliseconds: UInt32) {
        guard status == .play, interface != nil else { return }

        if let last = lastPositionSent {
            // Перемотка назад: переполнения UInt32 здесь не происходит
            // за счёт сравнения в обе стороны. В Windows-коде вычитание
            // давало подпоток при переходе через ноль.
            guard positionMilliseconds > last,
                  positionMilliseconds - last >= Self.positionIntervalMilliseconds else {
                return
            }
        }

        lastPositionSent = positionMilliseconds
        onCommand?(.play, positionMilliseconds)
    }

    /// Сбросить счётчик позиции: следующая отправка будет немедленной.
    ///
    /// Вызывается при старте воспроизведения и перемотке.
    public func resetPositionClock() {
        lastPositionSent = nil
    }

    /// Начать воспроизведение.
    public func play(positionMilliseconds: UInt32 = 0) {
        status = .play
        resetPositionClock()
        send(.play, positionMilliseconds: positionMilliseconds)
    }

    /// Пауза: команда уходит с текущей позицией, чтобы устройство
    /// знало, где остановилось.
    public func pause(positionMilliseconds: UInt32) {
        status = .pause
        send(.pause, positionMilliseconds: positionMilliseconds)
    }

    /// Остановка.
    public func stop(positionMilliseconds: UInt32 = 0) {
        status = .stop
        send(.stop, positionMilliseconds: positionMilliseconds)
    }
}