namespace PixelTuner.Audio.Vlc.Tests;

public class VlcAudioPlayerTests
{
    private static async Task<VlcAudioPlayer?> OpenAsync(double seconds)
    {
        if (VlcFixture.Get() is not { } library) return null;
        var player = new VlcAudioPlayer(library);
        await player.OpenAsync(() => new MemoryStream(VlcFixture.Wav(seconds)), "tone.wav");
        return player;
    }

    [Fact]
    public async Task Open_reads_the_length_and_waits_stopped_at_the_start()
    {
        if (await OpenAsync(3) is not { } player) return;
        await using var _ = player;

        Assert.InRange(player.Duration.TotalSeconds, 2.9, 3.1);
        Assert.Equal(AudioPlayerState.Stopped, player.State);
        Assert.Equal(TimeSpan.Zero, player.Position);
    }

    [Fact]
    public async Task Play_moves_the_position_and_pause_holds_it()
    {
        if (await OpenAsync(5) is not { } player) return;
        await using var _ = player;

        player.Play();
        Assert.True(await VlcFixture.WaitAsync(() => player.State == AudioPlayerState.Playing));
        Assert.True(await VlcFixture.WaitAsync(() => player.Position > TimeSpan.FromMilliseconds(400)));

        player.Pause();
        Assert.True(await VlcFixture.WaitAsync(() => player.State == AudioPlayerState.Paused));
        var held = player.Position;
        await Task.Delay(300);
        Assert.InRange((player.Position - held).TotalMilliseconds, -30, 30);
    }

    [Fact]
    public async Task Stop_goes_back_to_the_start()
    {
        if (await OpenAsync(5) is not { } player) return;
        await using var _ = player;
        player.Play();
        Assert.True(await VlcFixture.WaitAsync(() => player.Position > TimeSpan.FromMilliseconds(300)));

        player.Stop();

        Assert.True(await VlcFixture.WaitAsync(() => player.State == AudioPlayerState.Stopped));
        Assert.True(await VlcFixture.WaitAsync(() => player.Position < TimeSpan.FromMilliseconds(100)));
    }

    [Fact]
    public async Task Seek_before_playing_starts_playing_from_there()
    {
        if (await OpenAsync(10) is not { } player) return;
        await using var _ = player;

        player.Seek(TimeSpan.FromSeconds(6));
        Assert.Equal(TimeSpan.FromSeconds(6), player.Position);   // reported at once, though nothing plays yet

        player.Play();
        Assert.True(await VlcFixture.WaitAsync(() => player.State == AudioPlayerState.Playing));
        Assert.True(await VlcFixture.WaitAsync(() => player.Position >= TimeSpan.FromSeconds(5.9)));
    }

    [Fact]
    public async Task Seek_while_paused_moves_the_position_and_clamps()
    {
        if (await OpenAsync(10) is not { } player) return;
        await using var _ = player;
        player.Play();
        Assert.True(await VlcFixture.WaitAsync(() => player.State == AudioPlayerState.Playing));
        player.Pause();
        Assert.True(await VlcFixture.WaitAsync(() => player.State == AudioPlayerState.Paused));

        player.Seek(TimeSpan.FromSeconds(7));
        Assert.True(await VlcFixture.WaitAsync(() => Math.Abs((player.Position - TimeSpan.FromSeconds(7)).TotalMilliseconds) < 300));

        player.Seek(TimeSpan.FromSeconds(500));
        Assert.True(await VlcFixture.WaitAsync(() => player.Position <= player.Duration));
    }

    [Fact]
    public async Task Playing_to_the_end_raises_ended_and_stops()
    {
        if (await OpenAsync(1) is not { } player) return;
        await using var _ = player;
        bool ended = false;
        player.Ended += (_, _) => ended = true;

        player.Play();

        Assert.True(await VlcFixture.WaitAsync(() => ended, seconds: 8));
        Assert.True(await VlcFixture.WaitAsync(() => player.State == AudioPlayerState.Stopped));
    }

    [Fact]
    public async Task A_track_that_ended_plays_again_from_the_start()
    {
        if (await OpenAsync(1) is not { } player) return;
        await using var _ = player;
        int ended = 0;
        player.Ended += (_, _) => ended++;

        player.Play();
        Assert.True(await VlcFixture.WaitAsync(() => ended == 1, seconds: 8));
        player.Play();

        Assert.True(await VlcFixture.WaitAsync(() => ended == 2, seconds: 8));
    }

    [Fact]
    public async Task Something_that_is_not_a_sound_is_refused_on_open()
    {
        if (VlcFixture.Get() is not { } library) return;
        await using var player = new VlcAudioPlayer(library);

        await Assert.ThrowsAsync<AudioFormatException>(() =>
            player.OpenAsync(() => new MemoryStream("not audio"u8.ToArray()), "notes.mp3"));
    }

    [Fact]
    public async Task Volume_is_kept_between_zero_and_one()
    {
        if (await OpenAsync(2) is not { } player) return;
        await using var _ = player;

        player.Volume = 3;
        Assert.Equal(1, player.Volume);
        player.Volume = -1;
        Assert.Equal(0, player.Volume);
        player.Volume = 0.4;
        Assert.Equal(0.4, player.Volume);
    }

    [Fact]
    public async Task Opening_another_sound_replaces_the_first()
    {
        if (await OpenAsync(5) is not { } player) return;
        await using var _ = player;
        player.Play();
        Assert.True(await VlcFixture.WaitAsync(() => player.State == AudioPlayerState.Playing));

        await player.OpenAsync(() => new MemoryStream(VlcFixture.Wav(2)), "second.wav");

        Assert.InRange(player.Duration.TotalSeconds, 1.9, 2.1);
        Assert.Equal(AudioPlayerState.Stopped, player.State);
        Assert.Equal(TimeSpan.Zero, player.Position);
    }
}
