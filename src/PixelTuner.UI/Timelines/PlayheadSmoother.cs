namespace PixelTuner.UI.Timelines;

/// <summary>
/// Makes the playhead move evenly while the sound plays. A player may report its position in coarse steps (LibVLC: every 100 to 250 ms),
/// and a playhead that takes each report as it is moves in jerks. Between two reports the position is worked out from the clock;
/// a new report puts it right again, but never takes the playhead back by a little: it waits for the sound to catch up.
/// </summary>
public sealed class PlayheadSmoother(TimeProvider time)
{
    // A report that does not change for longer than this (the sound stalls) is not extrapolated any further.
    private static readonly TimeSpan MaxAhead = TimeSpan.FromMilliseconds(400);

    // A report this much behind what is shown is a drift of the two clocks; more is a jump (a seek) and is followed.
    private static readonly TimeSpan BackTolerance = TimeSpan.FromMilliseconds(250);

    private bool _running;
    private TimeSpan _reported;
    private long _reportedAt;
    private TimeSpan _shown;

    /// <summary>Forget the last report: the sound stopped, paused or was moved.</summary>
    public void Reset() => _running = false;

    /// <summary>The position to show now, given what the player reports.</summary>
    public TimeSpan Next(TimeSpan reported)
    {
        long now = time.GetTimestamp();
        if (!_running)
        {
            _running = true;
            _reported = reported;
            _reportedAt = now;
            return _shown = reported;
        }
        if (reported != _reported)
        {
            _reported = reported;
            _reportedAt = now;
        }
        var elapsed = time.GetElapsedTime(_reportedAt, now);
        var estimate = _reported + (elapsed < MaxAhead ? elapsed : MaxAhead);
        if (estimate < _shown && _shown - estimate < BackTolerance) estimate = _shown;
        return _shown = estimate;
    }
}
