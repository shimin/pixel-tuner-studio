namespace PixelTuner.Sync.Tests;

public class SyncSchedulerTests
{
    static TimeSpan S(double s) => TimeSpan.FromSeconds(s);

    [Fact]
    public void Playing_sends_position_once_per_second()
    {
        var s = new SyncScheduler();
        Assert.Equal("50 05 02 00 00 00 00 0D", Vectors.ToHex(s.OnStateChanged(PlaybackState.Playing, S(0), S(100))));

        Assert.Null(s.OnTick(S(0.5), S(100.5)));
        var packet = s.OnTick(S(1.0), S(101.0));
        Assert.Equal("50 05 02 E8 03 00 00 0D", Vectors.ToHex(packet!));
        Assert.Null(s.OnTick(S(1.9), S(101.9)));
        Assert.NotNull(s.OnTick(S(2.0), S(102.0)));
    }

    [Fact]
    public void Stopped_repeats_stop_at_zero()
    {
        var s = new SyncScheduler();
        Assert.Equal("50 05 04 00 00 00 00 0D", Vectors.ToHex(s.OnStateChanged(PlaybackState.Stopped, S(42), S(0))));
        Assert.Null(s.OnTick(S(42), S(0.5)));
        Assert.Equal("50 05 04 00 00 00 00 0D", Vectors.ToHex(s.OnTick(S(42), S(1))!));
    }

    [Fact]
    public void Pause_is_sent_once()
    {
        var s = new SyncScheduler();
        Assert.Equal("50 05 03 C8 AF 00 00 0D", Vectors.ToHex(s.OnStateChanged(PlaybackState.Paused, S(45), S(0))));
        Assert.Null(s.OnTick(S(45), S(5)));
    }

    [Fact]
    public void Output_latency_shifts_position()
    {
        var s = new SyncScheduler { OutputLatency = TimeSpan.FromMilliseconds(200) };
        Assert.Equal("50 05 02 C8 00 00 00 0D", Vectors.ToHex(s.OnStateChanged(PlaybackState.Playing, S(0), S(0))));
    }

    [Fact]
    public void Clock_going_back_does_not_stall_sending()
    {
        var s = new SyncScheduler();
        s.OnStateChanged(PlaybackState.Playing, S(0), S(100));
        // System clock jumped back: send on the next tick instead of waiting.
        Assert.NotNull(s.OnTick(S(0.1), S(50)));
    }
}
