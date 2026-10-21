using PixelTuner.Audio;
using PixelTuner.Core.Model;
using PixelTuner.Package;
using PixelTuner.UI.Timelines;

namespace PixelTuner.UI.Tests;

internal sealed class ManualTime : TimeProvider
{
    private long _ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => _ticks;
    public void Advance(TimeSpan by) => _ticks += by.Ticks;
}

public class TimelinePlaybackTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    private readonly ManualTime _time = new();
    private readonly ProjectDocument _doc = ProjectDocument.CreateNew("Show");
    private readonly TimelineEditor _editor;
    private readonly ClockAudioPlayer _player;

    public TimelinePlaybackTests()
    {
        _player = new ClockAudioPlayer(S(60), _time);
        _editor = new TimelineEditor(_doc) { ViewWidth = 1170, Player = _player, AudioDuration = S(60) };
    }

    private double X(double seconds) => _editor.TimeToX(S(seconds));

    [Fact]
    public void Play_starts_from_the_playhead_and_the_playhead_follows_the_sound()
    {
        _editor.SetPlayhead(S(10));

        _editor.PlayPause();
        _time.Advance(S(2.5));
        _editor.Tick();

        Assert.True(_editor.IsPlaying);
        Assert.Equal(12.5, _editor.Playhead.TotalSeconds, 2);
    }

    [Fact]
    public void Pause_holds_the_playhead_and_the_next_play_continues_there()
    {
        _editor.PlayPause();
        _time.Advance(S(4));
        _editor.Tick();

        _editor.PlayPause();                       // pause
        _time.Advance(S(10));                      // time passes while paused
        _editor.Tick();
        Assert.False(_editor.IsPlaying);
        Assert.Equal(4, _editor.Playhead.TotalSeconds, 2);

        _editor.PlayPause();                       // play again from 4
        _time.Advance(S(1));
        _editor.Tick();
        Assert.Equal(5, _editor.Playhead.TotalSeconds, 2);
    }

    [Fact]
    public void A_state_change_between_two_ticks_is_noticed_even_when_the_position_did_not_move()
    {
        _editor.PlayPause();
        _time.Advance(S(2));
        _editor.Tick();                            // playing, playhead at 2

        _editor.PlayPause();                       // paused at exactly 2: the position will not differ
        int raised = 0;
        _editor.Changed += (_, _) => raised++;
        _editor.Tick();

        Assert.False(_editor.IsPlaying);
        Assert.Equal(1, raised);                   // the redraw that flips the play button back
        _editor.Tick();
        Assert.Equal(1, raised);                   // and nothing more while nothing changes
    }

    [Fact]
    public void Clicking_the_ruler_or_the_wave_lane_moves_the_playhead_and_the_sound()
    {
        _editor.PointerDown(X(7), 10);
        _editor.PointerUp(X(7));
        Assert.Equal(S(7), _editor.Playhead);
        Assert.Equal(S(7), _player.Position);

        double waveY = TimelineEditor.RulerHeight + 20;
        Assert.Equal(HitKind.Wave, _editor.HitTest(X(3), waveY).Kind);
        _editor.PointerDown(X(3), waveY);
        _editor.PointerMove(X(4));
        _editor.PointerUp(X(4));
        Assert.Equal(S(4), _editor.Playhead);
        Assert.Equal(S(4), _player.Position);
        Assert.Null(_editor.Selected);
        Assert.False(_doc.IsDirty);                // the playhead is not part of the project
    }

    [Fact]
    public void The_playhead_cannot_go_past_the_end_of_the_sound()
    {
        _editor.SetPlayhead(S(500));
        Assert.Equal(S(60), _editor.Playhead);
    }

    [Fact]
    public void Stop_goes_back_to_the_start_and_scrolls_there()
    {
        _editor.ScrollTo(S(30));
        _editor.PlayPause();
        _time.Advance(S(3));
        _editor.Tick();

        _editor.Stop();

        Assert.Equal(TimeSpan.Zero, _editor.Playhead);
        Assert.Equal(TimeSpan.Zero, _editor.Scroll);
        Assert.False(_editor.IsPlaying);
    }

    [Fact]
    public void The_view_pages_along_when_the_playhead_leaves_it()
    {
        _editor.ZoomAt(4, TimelineEditor.HeaderWidth);          // 400 px/s: about 2.5 s in view
        _editor.PlayPause();

        _time.Advance(S(20));
        _editor.Tick();

        Assert.True(_editor.XToTime(TimelineEditor.HeaderWidth) <= _editor.Playhead);
        Assert.True(_editor.Playhead <= _editor.XToTime(_editor.ViewWidth));
    }

    [Fact]
    public void A_sound_that_plays_to_the_end_leaves_the_playhead_there_and_plays_again_from_the_start()
    {
        _editor.PlayPause();
        _time.Advance(S(59));
        _editor.Tick();

        _time.Advance(S(5));                       // past the end
        _editor.Tick();
        Assert.False(_editor.IsPlaying);
        Assert.Equal(S(60), _editor.Playhead);

        _editor.PlayPause();
        _time.Advance(S(1));
        _editor.Tick();
        Assert.True(_editor.IsPlaying);
        Assert.Equal(1, _editor.Playhead.TotalSeconds, 2);
    }

    [Fact]
    public void Without_a_player_the_playhead_still_moves_and_play_does_nothing()
    {
        var editor = new TimelineEditor(_doc) { ViewWidth = 1170 };

        editor.SetPlayhead(S(9));
        editor.PlayPause();
        editor.Tick();

        Assert.Equal(S(9), editor.Playhead);
        Assert.False(editor.IsPlaying);
    }

    [Fact]
    public void The_sound_lane_sits_between_the_ruler_and_the_timelines()
    {
        _doc.Project.Timelines.Add(new Timeline { Name = "T" });
        Assert.Equal(TimelineEditor.RulerHeight + TimelineEditor.WaveHeight, _editor.TrackTop(0));
        Assert.Equal(HitKind.Header, _editor.HitTest(20, _editor.TrackTop(0) + 5).Kind);
        Assert.Equal(HitKind.None, _editor.HitTest(20, TimelineEditor.RulerHeight + 5).Kind);   // the name column of the sound lane
        Assert.Equal(_editor.TrackTop(0) + TimelineEditor.TrackHeight, _editor.TotalHeight);
    }
}
