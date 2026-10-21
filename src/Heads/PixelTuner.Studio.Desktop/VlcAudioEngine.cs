using PixelTuner.Audio;
using PixelTuner.Audio.Vlc;
using PixelTuner.UI.Services;

namespace PixelTuner.Studio.Desktop;

/// <summary>Sound for Studio: LibVLC, one engine for playing and for reading the waveform.</summary>
sealed class VlcAudioEngine(VlcLibrary library) : IAudioEngine
{
    public IAudioPlayer CreatePlayer() => new VlcAudioPlayer(library);

    public IAudioDecoder CreateDecoder() => new VlcAudioDecoder(library);

    /// <summary>The engine, or null when LibVLC cannot be loaded here: Studio then works without sound.</summary>
    public static VlcAudioEngine? TryCreate() => VlcLibrary.Shared is { } library ? new VlcAudioEngine(library) : null;
}
