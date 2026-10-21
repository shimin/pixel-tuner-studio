using PixelTuner.Audio;

namespace PixelTuner.UI.Services;

/// <summary>
/// What plays and reads sound on this platform. The head sets <see cref="Current"/> before the UI starts
/// (LibVLC on Windows and macOS, the system players on phones); the shared UI knows only the contracts.
/// </summary>
public interface IAudioEngine
{
    IAudioPlayer CreatePlayer();
    IAudioDecoder CreateDecoder();
}

public static class AudioEngine
{
    /// <summary>Null when the platform has no sound engine (LibVLC missing): the app works without sound.</summary>
    public static IAudioEngine? Current { get; set; }
}
