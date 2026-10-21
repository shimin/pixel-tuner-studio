namespace PixelTuner.Audio;

/// <summary>
/// A player without sound: a clock that runs like a soundtrack of the given length. For
/// shows without music (the devices still need a position) and for testing the sync.
/// </summary>
public sealed class ClockAudioPlayer(TimeSpan duration, TimeProvider? time = null) : IAudioPlayer
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly Lock _sync = new();
    private TimeSpan _offset;
    private long? _startedAt;
    private AudioPlayerState _state;
    private bool _ended;

    public TimeSpan Duration => duration;
    public double Volume { get; set; } = 1;
    public TimeSpan OutputLatency => TimeSpan.Zero;

    public event EventHandler<AudioPlayerState>? StateChanged;
    public event EventHandler? Ended;

    public Task OpenAsync(Func<Stream> openStream, string fileName, CancellationToken cancel = default)
    {
        Stop();
        return Task.CompletedTask;
    }

    /// <summary>The clock position; reaching the end stops the clock and raises <see cref="Ended"/>.</summary>
    public TimeSpan Position
    {
        get
        {
            bool ended;
            TimeSpan position;
            lock (_sync)
            {
                position = Current();
                ended = _state == AudioPlayerState.Playing && position >= duration;
                if (ended)
                {
                    _state = AudioPlayerState.Stopped;
                    _offset = TimeSpan.Zero;
                    _startedAt = null;
                    position = TimeSpan.Zero;
                }
            }
            if (ended)
            {
                StateChanged?.Invoke(this, AudioPlayerState.Stopped);
                if (!_ended) { _ended = true; Ended?.Invoke(this, EventArgs.Empty); }
            }
            return position;
        }
    }

    public AudioPlayerState State
    {
        get
        {
            _ = Position;   // lets the end of the track take effect
            lock (_sync) return _state;
        }
    }

    public void Play() => Change(AudioPlayerState.Playing, () =>
    {
        _ended = false;
        _startedAt = _time.GetTimestamp();
    });

    public void Pause() => Change(AudioPlayerState.Paused, () =>
    {
        _offset = Current();
        _startedAt = null;
    });

    public void Stop()
    {
        // Back to the start even when already stopped (after a seek).
        lock (_sync)
        {
            _offset = TimeSpan.Zero;
            _startedAt = null;
        }
        Change(AudioPlayerState.Stopped, () => { });
    }

    public void Seek(TimeSpan position)
    {
        lock (_sync)
        {
            _offset = position < TimeSpan.Zero ? TimeSpan.Zero : position > duration ? duration : position;
            if (_startedAt is not null) _startedAt = _time.GetTimestamp();
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private TimeSpan Current()
    {
        var position = _offset + (_startedAt is { } start ? _time.GetElapsedTime(start) : TimeSpan.Zero);
        return position > duration ? duration : position;
    }

    private void Change(AudioPlayerState state, Action apply)
    {
        lock (_sync)
        {
            if (_state == state) return;
            apply();
            _state = state;
        }
        StateChanged?.Invoke(this, state);
    }
}
