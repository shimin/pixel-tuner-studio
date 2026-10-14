import Foundation

public enum TimeFormat {
    /// `mm:ss`, после часа — `h:mm:ss`. Секунды отбрасываются, как в
    /// `TimeSpan.ToString(@"mm\:ss")`. Windows-формат после 59:59
    /// начинал заново с 00:00.
    public static func clock(_ seconds: TimeInterval) -> String {
        guard seconds.isFinite, seconds > 0 else { return "00:00" }

        let total = Int(seconds.rounded(.down))
        let hours = total / 3600
        let minutes = total % 3600 / 60
        let secs = total % 60

        if hours > 0 {
            return String(format: "%d:%02d:%02d", hours, minutes, secs)
        }
        return String(format: "%02d:%02d", minutes, secs)
    }

    /// Позиция для пакета: миллисекунды, без отрицательных значений
    /// и переполнения (расхождение 5 в plans/protocol.md).
    public static func milliseconds(_ seconds: TimeInterval) -> UInt32 {
        guard seconds.isFinite, seconds > 0 else { return 0 }
        let value = (seconds * 1000).rounded(.down)
        guard value < Double(UInt32.max) else { return UInt32.max }
        return UInt32(value)
    }
}
