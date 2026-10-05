import AVFoundation
import Foundation

/// Воспроизведение аудиофайла.
///
/// Заменяет WPF `System.Windows.Media.MediaPlayer`, который на macOS
/// не существует. Основа — `AVAudioPlayer`: умеет позицию, длительность,
/// play / pause / stop и громкость, то есть ровно то, чем пользуется
/// текущий код.
///
/// - Note: `AVAudioPlayer` загружает файл целиком в память. Для треков
///   это нормально, для длинных записей лучше `AVPlayer`.
public final class AudioPlayer {
    private var player: AVAudioPlayer?

    /// Позиция воспроизведения, секунды.
    public var position: Double {
        get { player?.currentTime ?? 0 }
        set {
            guard let player = player else { return }
            // Отрицательное значение отсекается: AVFoundation падает на нём.
            player.currentTime = max(0, newValue)
        }
    }

    /// Длительность трека, секунды. `nil`, пока файл не загружен.
    public var duration: Double? {
        guard let player = player, player.duration > 0 else { return nil }
        return player.duration
    }

    /// Громкость 0...1. Windows-версия использовала тот же диапазон.
    public var volume: Double {
        get { player.map { Double($0.volume) } ?? Double(pendingVolume) }
        set {
            // Настройка применяется и к текущему файлу, и к следующим:
            // в Windows-версии громкость ставилась в обработчике ползунка,
            // поэтому сохраняется при смене трека.
            pendingVolume = Float(min(max(newValue, 0), 1))
            player?.volume = pendingVolume
        }
    }

    private var pendingVolume: Float = 0.5

        public init() {}

    public var isPlaying: Bool { player?.isPlaying ?? false }

    /// Открывает аудиофайл.
    ///
    /// - Throws: если файл не найден или не содержит аудио.
    public func open(url: URL) throws {
        close()

        // Создаём экземпляр явно: у AVFoundation своя категория, а
        // `AVAudioPlayer(contentsOf:)` бросает исключение Objective-C,
        // которое в Swift не перехватывается как обычная ошибка.
        guard let player = try? AVAudioPlayer(contentsOf: url) else {
            throw AudioError.cannotOpen(url)
        }

        player.volume = pendingVolume
        // Играть нельзя до `prepareToPlay`, иначе позиция и длительность
        // недоступны сразу после открытия.
        guard player.prepareToPlay() else {
            throw AudioError.cannotOpen(url)
        }

        self.player = player
    }

    public func play() {
        player?.play()
    }

    public func pause() {
        player?.pause()
    }

    public func stop() {
        player?.stop()
        player?.currentTime = 0
    }

    public func close() {
        player?.stop()
        player = nil
    }
}

public enum AudioError: Error, CustomStringConvertible {
    case cannotOpen(URL)

    public var description: String {
        switch self {
        case .cannotOpen(let url):
            return "Не удалось открыть аудиофайл: \(url.lastPathComponent)"
        }
    }
}

/// Класс для доступа к AVAudioPlayer из Swift.
///
/// `AVAudioPlayer` требует категорию, иначе Swift-инициализаторы
/// недоступны. Это безопасный обход: категория ничего не меняет в
/// поведении, только открывает инициализаторы.
@objc(EpicAudioPlayer)
public class EpicAudioPlayer: AVAudioPlayer {
}