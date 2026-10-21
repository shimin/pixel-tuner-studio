namespace PixelTuner.Sync;

/// <summary>
/// Decides which playback packets to send and when. Pure logic with no sockets and
/// no timers, so it is the same on every platform and fully testable.
///
/// Behaviour (protocol.md, section 2):
/// - while playing, the current position goes out once per <see cref="PositionInterval"/>;
/// - while stopped, a stop at 0 is repeated once per <see cref="StopRepeatInterval"/>,
///   so a device that missed the stop still gets it;
/// - pause is sent once and not repeated.
/// Seeking backwards is handled explicitly (no unsigned underflow as in PixelTuner 2).
/// </summary>
public sealed class SyncScheduler
{
    public TimeSpan PositionInterval { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan StopRepeatInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Shift added to the audio position before it is sent, to compensate
    /// output latency (Bluetooth speakers, external mixers). May be negative.
    /// </summary>
    public TimeSpan OutputLatency { get; set; }

    PlaybackState _state = PlaybackState.Stopped;
    TimeSpan? _lastSentAt;

    public PlaybackState State => _state;

    /// <summary>Call on play, pause, stop and seek. Returns the packet to send now.</summary>
    public byte[] OnStateChanged(PlaybackState state, TimeSpan audioPosition, TimeSpan now)
    {
        _state = state;
        _lastSentAt = now;
        return state switch
        {
            PlaybackState.Playing => Packet(PlaybackCommand.Play, audioPosition),
            PlaybackState.Paused => Packet(PlaybackCommand.Pause, audioPosition),
            _ => ShowProtocol.EncodePlayback(PlaybackCommand.Stop, 0),
        };
    }

    /// <summary>
    /// Call on every clock tick (any rate, for example 10–60 times a second).
    /// Returns a packet when one is due, otherwise null.
    /// </summary>
    public byte[]? OnTick(TimeSpan audioPosition, TimeSpan now)
    {
        if (_state == PlaybackState.Paused)
            return null;

        var interval = _state == PlaybackState.Playing ? PositionInterval : StopRepeatInterval;
        if (_lastSentAt is { } last && now >= last && now - last < interval)
            return null;

        _lastSentAt = now;
        return _state == PlaybackState.Playing
            ? Packet(PlaybackCommand.Play, audioPosition)
            : ShowProtocol.EncodePlayback(PlaybackCommand.Stop, 0);
    }

    byte[] Packet(PlaybackCommand command, TimeSpan audioPosition) =>
        ShowProtocol.EncodePlayback(command, ShowProtocol.ToPacketPosition(audioPosition + OutputLatency));
}

public enum PlaybackState
{
    Stopped,
    Playing,
    Paused,
}
