using PixelTuner.Audio;
using PixelTuner.UI.Timelines;

namespace PixelTuner.UI.Tests;

public class PlayheadSmootherTests
{
    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    private readonly ManualTime _time = new();

    // ---- the smoother

    [Fact]
    public void Between_two_reports_the_position_moves_with_the_clock()
    {
        var smoother = new PlayheadSmoother(_time);
        Assert.Equal(Ms(1000), smoother.Next(Ms(1000)));
        _time.Advance(Ms(33));
        Assert.Equal(Ms(1033), smoother.Next(Ms(1000)));    // the player still says 1000
        _time.Advance(Ms(33));
        Assert.Equal(Ms(1066), smoother.Next(Ms(1000)));
    }

    [Fact]
    public void A_player_that_reports_every_quarter_of_a_second_gives_a_position_that_never_goes_back_and_never_stands_still()
    {
        var smoother = new PlayheadSmoother(_time);
        var last = smoother.Next(Ms(0));
        // The sound plays at its speed; the player knows its position in steps of 250 ms, seen a little late.
        for (int i = 1; i <= 120; i++)
        {
            _time.Advance(Ms(33));
            double truth = i * 33;
            var shown = smoother.Next(Ms(Math.Floor(truth / 250) * 250));
            Assert.True(shown > last, $"step {i}: {shown} after {last}");
            Assert.InRange((shown - Ms(truth)).TotalMilliseconds, -270, 270);
            last = shown;
        }
    }

    [Fact]
    public void A_report_that_is_a_little_behind_waits_for_the_sound_and_a_jump_is_followed()
    {
        var smoother = new PlayheadSmoother(_time);
        smoother.Next(Ms(1000));
        _time.Advance(Ms(200));
        Assert.Equal(Ms(1200), smoother.Next(Ms(1000)));
        Assert.Equal(Ms(1200), smoother.Next(Ms(1100)));    // 100 ms behind: held
        Assert.Equal(Ms(5000), smoother.Next(Ms(5000)));    // a seek
        Assert.Equal(Ms(100), smoother.Next(Ms(100)));      // and back
    }

    [Fact]
    public void A_sound_that_stalls_is_not_run_ahead_for_ever_and_reset_takes_the_next_report_as_it_is()
    {
        var smoother = new PlayheadSmoother(_time);
        smoother.Next(Ms(1000));
        _time.Advance(Ms(5000));
        Assert.Equal(Ms(1400), smoother.Next(Ms(1000)));

        smoother.Reset();
        Assert.Equal(Ms(900), smoother.Next(Ms(900)));
    }

    // ---- the editor

    private sealed class SteppedPlayer(ManualTime time, TimeSpan step) : IAudioPlayer
    {
        private readonly long _start = time.GetTimestamp();
        public AudioPlayerState State { get; private set; } = AudioPlayerState.Playing;
        public TimeSpan Duration => TimeSpan.FromMinutes(1);
        public TimeSpan Position => TimeSpan.FromTicks(time.GetElapsedTime(_start, time.GetTimestamp()).Ticks / step.Ticks * step.Ticks);
        public double Volume { get; set; }
        public TimeSpan OutputLatency => TimeSpan.Zero;
        public void Play() { }
        public void Pause() => State = AudioPlayerState.Paused;
        public void Stop() => State = AudioPlayerState.Stopped;
        public void Seek(TimeSpan position) { }
        public event EventHandler<AudioPlayerState>? StateChanged { add { } remove { } }
        public event EventHandler? Ended { add { } remove { } }
        public Task OpenAsync(Func<Stream> openStream, string fileName, CancellationToken cancel = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public void The_playhead_of_the_editor_moves_evenly_over_a_player_that_reports_in_steps_and_stops_where_the_player_pauses()
    {
        using var doc = Package.ProjectDocument.CreateNew("Show");
        var player = new SteppedPlayer(_time, Ms(250));
        var editor = new TimelineEditor(doc) { ViewWidth = 1170, Player = player, AudioDuration = TimeSpan.FromMinutes(1), Time = _time };

        var seen = new List<TimeSpan>();
        for (int i = 0; i < 30; i++)
        {
            _time.Advance(Ms(33));
            editor.Tick();
            seen.Add(editor.Playhead);
        }
        Assert.Equal(30, seen.Distinct().Count());                   // a new place at every tick
        Assert.Equal(seen.Order(), seen);                             // never back
        Assert.InRange(seen[^1].TotalMilliseconds, 750, 1250);        // and close to the 990 ms that have passed

        player.Pause();
        _time.Advance(Ms(33));
        editor.Tick();
        Assert.Equal(Ms(1000), editor.Playhead);                      // what the paused player says (1023 ms in steps of 250)
    }
}
