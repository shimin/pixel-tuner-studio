using PixelTuner.Audio;
using PixelTuner.Core.Model;
using PixelTuner.Package;
using PixelTuner.UI.Services;
using PixelTuner.UI.ViewModels;

namespace PixelTuner.UI.Tests;

internal sealed class FakeAudioEngine(Exception? decoderFails = null) : IAudioEngine
{
    public ClockAudioPlayer? Player { get; private set; }
    public int Decoded { get; private set; }

    public IAudioPlayer CreatePlayer() => Player = new ClockAudioPlayer(TimeSpan.FromSeconds(10));

    public IAudioDecoder CreateDecoder() => new FakeDecoder(this, decoderFails);

    private sealed class FakeDecoder(FakeAudioEngine engine, Exception? fails) : IAudioDecoder
    {
        public Task<Waveform> ReadWaveformAsync(Stream source, string fileName, int pointsPerSecond = Waveform.DefaultPointsPerSecond,
            CancellationToken cancel = default)
        {
            engine.Decoded++;
            if (fails is not null) throw fails;
            float[] min = Enumerable.Repeat(-0.5f, 10 * pointsPerSecond).ToArray();
            float[] max = Enumerable.Repeat(0.5f, 10 * pointsPerSecond).ToArray();
            return Task.FromResult(new Waveform(pointsPerSecond, TimeSpan.FromSeconds(10), min, max));
        }
    }
}

public class EditorAudioTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ui-audio").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private ProjectDocument DocumentWithSound(out string soundId)
    {
        var writer = new PxPackageWriter();
        soundId = writer.AddMedia([1, 2, 3, 4], "song.mp3");
        string path = Path.Combine(_dir, "show.pxpkg");
        writer.Save(path, new ShowProject { Name = "Show", AudioResourceId = soundId, AudioSourcePath = @"C:\music\song.mp3" });
        return ProjectDocument.Open(path);
    }

    private static async Task<bool> WaitAsync(Func<bool> condition)
    {
        for (int i = 0; i < 200 && !condition(); i++) await Task.Delay(10);
        return condition();
    }

    [Fact]
    public async Task A_project_with_a_soundtrack_loads_the_player_and_the_waveform()
    {
        using var doc = DocumentWithSound(out _);
        var engine = new FakeAudioEngine();

        using var editor = new EditorViewModel(doc, engine);
        Assert.True(await WaitAsync(() => editor.Timelines.Wave is not null));

        Assert.True(editor.HasSoundtrack);
        Assert.Equal("song.mp3", editor.SoundtrackName);
        Assert.Equal(TimeSpan.FromSeconds(10), editor.Timelines.AudioDuration);
        Assert.True(editor.CanPlay);
        Assert.Null(editor.Message);
        Assert.Equal("0:00.000 / 0:10.000", editor.TimeText);
        Assert.Same(engine.Player, editor.Timelines.Player);
    }

    [Fact]
    public async Task Play_and_stop_commands_drive_the_player_and_the_glyph()
    {
        using var doc = DocumentWithSound(out _);
        var engine = new FakeAudioEngine();
        using var editor = new EditorViewModel(doc, engine);
        Assert.True(await WaitAsync(() => editor.CanPlay));
        Assert.Equal("▶", editor.PlayPauseGlyph);

        editor.PlayPauseCommand.Execute(null);
        Assert.Equal(AudioPlayerState.Playing, engine.Player!.State);
        Assert.Equal("⏸", editor.PlayPauseGlyph);

        editor.StopCommand.Execute(null);
        Assert.Equal(AudioPlayerState.Stopped, engine.Player.State);
        Assert.Equal("▶", editor.PlayPauseGlyph);
        Assert.Equal(TimeSpan.Zero, editor.Timelines.Playhead);
    }

    [Fact]
    public async Task The_glyph_returns_to_play_after_a_pause()
    {
        using var doc = DocumentWithSound(out _);
        var engine = new FakeAudioEngine();
        using var editor = new EditorViewModel(doc, engine);
        Assert.True(await WaitAsync(() => editor.CanPlay));
        var changed = new List<string?>();
        editor.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        editor.PlayPauseCommand.Execute(null);
        editor.Timelines.Tick();                    // the view's timer sees it playing
        Assert.Equal("⏸", editor.PlayPauseGlyph);

        editor.PlayPauseCommand.Execute(null);      // pause
        changed.Clear();
        editor.Timelines.Tick();                    // the timer sees it paused

        Assert.Contains(nameof(EditorViewModel.PlayPauseGlyph), changed);
        Assert.Equal("▶", editor.PlayPauseGlyph);
    }

    [Fact]
    public async Task Choosing_a_soundtrack_adds_it_to_the_project_and_undo_takes_it_back()
    {
        string sound = Path.Combine(_dir, "new.wav");
        File.WriteAllBytes(sound, [9, 9, 9]);
        using var doc = ProjectDocument.CreateNew("Show");
        var engine = new FakeAudioEngine();
        var picker = new FakePicker { AudioPath = sound };
        using var editor = new EditorViewModel(doc, engine, () => picker);
        Assert.False(editor.HasSoundtrack);
        Assert.False(editor.CanPlay);

        await editor.SetSoundtrackCommand.ExecuteAsync(null);
        Assert.True(await WaitAsync(() => editor.Timelines.Wave is not null));

        Assert.True(editor.HasSoundtrack);
        Assert.Equal("new.wav", editor.SoundtrackName);
        Assert.True(doc.IsDirty);
        Assert.True(editor.CanPlay);

        doc.Undo();
        Assert.True(await WaitAsync(() => editor.Timelines.Wave is null));
        Assert.False(editor.HasSoundtrack);
        Assert.False(editor.CanPlay);

        doc.Redo();
        Assert.True(await WaitAsync(() => editor.Timelines.Wave is not null));
    }

    [Fact]
    public async Task Cancelling_the_file_dialog_changes_nothing_and_remove_takes_the_soundtrack_out()
    {
        using var doc = DocumentWithSound(out _);
        var picker = new FakePicker();   // the user cancels
        using var editor = new EditorViewModel(doc, new FakeAudioEngine(), () => picker);
        Assert.True(await WaitAsync(() => editor.CanPlay));

        await editor.SetSoundtrackCommand.ExecuteAsync(null);
        Assert.False(doc.IsDirty);

        editor.RemoveSoundtrackCommand.Execute(null);
        Assert.False(editor.HasSoundtrack);
        Assert.True(await WaitAsync(() => editor.Timelines.Wave is null));
        Assert.False(editor.RemoveSoundtrackCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_soundtrack_that_is_not_in_the_project_is_named_in_a_message()
    {
        // An imported project whose sound file was not found keeps the id of nothing and the path it had.
        using var doc = ProjectDocument.CreateNew("Imported");
        doc.Project.AudioResourceId = "deadbeef";
        doc.Project.AudioSourcePath = @"D:\shows\missing.mp3";

        using var editor = new EditorViewModel(doc, new FakeAudioEngine());
        await Task.Delay(50);

        Assert.True(editor.HasMessage);
        Assert.Contains("missing.mp3", editor.Message);
        Assert.False(editor.CanPlay);
    }

    [Fact]
    public async Task Without_a_sound_engine_the_editor_says_so_and_stays_silent()
    {
        using var doc = DocumentWithSound(out _);

        using var editor = new EditorViewModel(doc, engine: null);
        await Task.Delay(50);

        Assert.True(editor.HasMessage);
        Assert.Contains("LibVLC", editor.Message);
        Assert.False(editor.CanPlay);
        editor.PlayPauseCommand.Execute(null);   // does nothing, does not throw
    }

    [Fact]
    public async Task A_sound_that_cannot_be_read_is_reported()
    {
        using var doc = DocumentWithSound(out _);
        var engine = new FakeAudioEngine(new AudioFormatException("not a sound"));

        using var editor = new EditorViewModel(doc, engine);
        Assert.True(await WaitAsync(() => editor.HasMessage));

        Assert.Contains("not a sound", editor.Message);
        Assert.False(editor.CanPlay);
        Assert.True(editor.HasSoundtrack);   // the project still refers to it
    }

    [Fact]
    public async Task Closing_the_editor_while_the_sound_loads_is_harmless()
    {
        using var doc = DocumentWithSound(out _);
        var editor = new EditorViewModel(doc, new FakeAudioEngine());
        editor.Dispose();
        await Task.Delay(50);   // the load that was in flight ends quietly
        Assert.NotNull(editor);
    }
}
