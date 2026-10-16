import EpicPlayerKit
import Foundation

/// Таймеры модели. Режим `.common`: в режиме `.default` таймер замирает,
/// пока пользователь держит меню или тянет окно, и позиция перестаёт
/// уходить на устройство (подводный камень 4 в plans/porting-plan.md).
@MainActor
final class PlaybackClock {
    private var timers: [Timer] = []

    func start(_ model: PlayerModel) {
        stop()
        timers = [
            schedule(every: 0.25) { model.refresh() },
            schedule(every: 1.0) { model.networkTick() },
        ]
    }

    func stop() {
        timers.forEach { $0.invalidate() }
        timers.removeAll()
    }

    private func schedule(every interval: TimeInterval, _ action: @escaping @MainActor () -> Void) -> Timer {
        let timer = Timer(timeInterval: interval, repeats: true) { _ in
            MainActor.assumeIsolated(action)
        }
        timer.tolerance = interval / 10
        RunLoop.main.add(timer, forMode: .common)
        return timer
    }
}
