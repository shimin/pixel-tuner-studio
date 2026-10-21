namespace PixelTuner.Audio;

public enum AudioPlayerState
{
    /// <summary>Nothing open, or opened and at the start.</summary>
    Stopped,
    Playing,
    Paused,
}

/// <summary>
/// Plays the show's soundtrack. The devices follow <see cref="Position"/>, so an
/// implementation reports what is heard, as precisely as the platform allows.
/// Members may be called from any thread; events may come on any thread.
/// </summary>
public interface IAudioPlayer : IAsyncDisposable
{
    /// <summary>
    /// Opens a soundtrack. The stream comes from the package; an implementation that needs
    /// a file copies it. <paramref name="fileName"/> tells the format (".mp3", ".wav").
    /// Ends in <see cref="AudioPlayerState.Stopped"/> at the start.
    /// </summary>
    /// <exception cref="AudioFormatException">The data is not a sound the platform can play.</exception>
    Task OpenAsync(Func<Stream> openStream, string fileName, CancellationToken cancel = default);

    /// <summary>Length of the soundtrack; zero until opened.</summary>
    TimeSpan Duration { get; }

    /// <summary>The point of the soundtrack being heard now.</summary>
    TimeSpan Position { get; }

    AudioPlayerState State { get; }

    /// <summary>0 to 1.</summary>
    double Volume { get; set; }

    /// <summary>
    /// How much later than <see cref="Position"/> the sound leaves the speaker, as the system
    /// reports it (Bluetooth speakers can add 100–300 ms). The sync adds it and the user's own
    /// correction to the position it sends to the devices.
    /// </summary>
    TimeSpan OutputLatency { get; }

    void Play();
    void Pause();

    /// <summary>Stops and goes back to the start.</summary>
    void Stop();

    /// <summary>Moves to a position (clamped to the soundtrack) without changing the state.</summary>
    void Seek(TimeSpan position);

    /// <summary>Play, pause or stop: also when the track ends (Stopped) or the system interrupts it (Paused).</summary>
    event EventHandler<AudioPlayerState>? StateChanged;

    /// <summary>The soundtrack played to its end.</summary>
    event EventHandler? Ended;
}

/// <summary>Reads a soundtrack into the waveform the timelines are drawn over.</summary>
public interface IAudioDecoder
{
    /// <exception cref="AudioFormatException">The data is not a sound the decoder understands.</exception>
    Task<Waveform> ReadWaveformAsync(Stream source, string fileName, int pointsPerSecond = Waveform.DefaultPointsPerSecond,
        CancellationToken cancel = default);
}

/// <summary>The soundtrack is damaged or in a format the platform cannot play.</summary>
public sealed class AudioFormatException(string message, Exception? inner = null) : Exception(message, inner);
