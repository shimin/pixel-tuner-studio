import AVFoundation
import Foundation

/// `AudioEngine` на `AVAudioPlayer`. См. plans/app-design.md, раздел 4.
@MainActor
public final class SystemAudioPlayer: AudioEngine {
    private var player: AVAudioPlayer?
    private let delegate = FinishDelegate()

    public var onFinish: (() -> Void)?

    public var volume: Float = 0.5 {
        didSet { player?.volume = volume }
    }

    public init() {
        delegate.onFinish = { [weak self] in self?.onFinish?() }
    }

    public var isLoaded: Bool { player != nil }
    public var duration: TimeInterval { player?.duration ?? 0 }
    public var currentTime: TimeInterval { player?.currentTime ?? 0 }
    public var isPlaying: Bool { player?.isPlaying ?? false }

    /// `contentsOf:` читает файл потоково, целиком в память не грузит.
    /// `prepareToPlay()` заранее заполняет буферы — замена «разблокировки»
    /// play + stop на нулевой громкости из Windows-версии.
    public func load(_ url: URL) throws {
        player?.stop()
        player = nil

        let loaded = try AVAudioPlayer(contentsOf: url)
        loaded.delegate = delegate
        loaded.volume = volume
        loaded.prepareToPlay()
        player = loaded
    }

    public func play() {
        player?.play()
    }

    public func pause() {
        player?.pause()
    }

    /// `AVAudioPlayer.stop()` позицию не сбрасывает, в отличие от WPF.
    public func stop() {
        guard let player else { return }
        player.stop()
        player.currentTime = 0
        player.prepareToPlay()
    }
}

private final class FinishDelegate: NSObject, AVAudioPlayerDelegate {
    var onFinish: (@MainActor () -> Void)?

    func audioPlayerDidFinishPlaying(_ player: AVAudioPlayer, successfully flag: Bool) {
        let callback = onFinish
        DispatchQueue.main.async {
            MainActor.assumeIsolated { callback?() }
        }
    }
}
