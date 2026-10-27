namespace PixelTuner.Audio.Tests;

public class WaveformTests
{
    [Fact]
    public void Peaks_are_per_point_with_channels_mixed()
    {
        // 1000 Hz, stereo, 100 points per second: 10 frames per point.
        var builder = new WaveformBuilder(1000, 2, 100);
        var samples = new float[2 * 25];
        for (int frame = 0; frame < 25; frame++)
        {
            samples[2 * frame] = frame / 100f;           // left rises
            samples[2 * frame + 1] = -frame / 100f * 3;  // right falls faster
        }
        // Frames split across calls must not matter.
        builder.Add(samples.AsSpan(0, 7));
        builder.Add(samples.AsSpan(7));
        var waveform = builder.Build();

        Assert.Equal(3, waveform.Count);   // 10 + 10 + 5 frames
        Assert.Equal(TimeSpan.FromMilliseconds(25), waveform.Duration);
        // mixed = (f - 3f) / 2 / 100 = -f / 100
        Assert.Equal(-0.09f, waveform.Minimum[0], 5);
        Assert.Equal(0f, waveform.Maximum[0], 5);
        Assert.Equal(-0.24f, waveform.Minimum[2], 5);
        Assert.Equal((-0.24f, -0.10f), Round(waveform.Range(TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(30))));
        Assert.Equal((0f, 0f), waveform.Range(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(6)));
    }

    [Fact]
    public void The_loudest_sample_of_each_channel_is_kept_per_point()
    {
        var builder = new WaveformBuilder(1000, 2, 100);
        var samples = new float[2 * 20];
        for (int frame = 0; frame < 20; frame++)
        {
            samples[2 * frame] = frame == 3 ? -0.5f : 0.1f;      // left: one loud (negative) sample in the first point
            samples[2 * frame + 1] = frame == 15 ? 0.8f : 0.2f;  // right: one loud sample in the second
        }
        builder.Add(samples);
        var waveform = builder.Build();

        Assert.Equal([0.5f, 0.1f], waveform.Left);
        Assert.Equal([0.2f, 0.8f], waveform.Right);
        Assert.Equal((0.5f, 0.8f), waveform.Peaks(TimeSpan.Zero, TimeSpan.FromMilliseconds(20)));
        Assert.Equal((0.1f, 0.8f), waveform.Peaks(TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(20)));
        Assert.Equal((0f, 0f), waveform.Peaks(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(6)));
    }

    [Fact]
    public void A_mono_sound_is_the_same_in_both_channels_and_an_outline_without_channels_is_taken_for_both()
    {
        var builder = new WaveformBuilder(1000, 1, 100);
        builder.Add(new float[] { 0.1f, -0.7f, 0.2f, 0f, 0f, 0f, 0f, 0f, 0f, 0f });
        var mono = builder.Build();
        Assert.Equal([0.7f], mono.Left);
        Assert.Equal(mono.Left, mono.Right);

        var plain = new Waveform(100, TimeSpan.FromSeconds(1), [-0.4f, -0.1f], [0.3f, 0.2f]);
        Assert.Equal([0.4f, 0.2f], plain.Left);
        Assert.Same(plain.Left, plain.Right);
    }

    [Fact]
    public void Pcm16_is_scaled_to_minus_one_to_one()
    {
        var builder = new WaveformBuilder(8000, 1, 8000);
        builder.Add(new short[] { short.MinValue, 0, 16384, short.MaxValue });
        var waveform = builder.Build();
        Assert.Equal([-1f, 0f, 0.5f, 32767 / 32768f], waveform.Maximum);
    }

    private static (float, float) Round((float Min, float Max) r) => (MathF.Round(r.Min, 5), MathF.Round(r.Max, 5));
}

public class ClockAudioPlayerTests
{
    private sealed class ManualTime : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public void Advance(TimeSpan by) => _ticks += by.Ticks;
    }

    [Fact]
    public async Task Runs_like_a_soundtrack()
    {
        var time = new ManualTime();
        await using var player = new ClockAudioPlayer(TimeSpan.FromSeconds(10), time);
        var states = new List<AudioPlayerState>();
        int ended = 0;
        player.StateChanged += (_, s) => states.Add(s);
        player.Ended += (_, _) => ended++;

        player.Play();
        time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(TimeSpan.FromSeconds(2), player.Position);

        player.Pause();
        time.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(TimeSpan.FromSeconds(2), player.Position);

        player.Seek(TimeSpan.FromSeconds(8));
        player.Play();
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.FromSeconds(9), player.Position);

        // The end stops the clock and goes back to the start, once.
        time.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(AudioPlayerState.Stopped, player.State);
        Assert.Equal(TimeSpan.Zero, player.Position);
        Assert.Equal(1, ended);
        Assert.Equal([AudioPlayerState.Playing, AudioPlayerState.Paused, AudioPlayerState.Playing, AudioPlayerState.Stopped], states);

        player.Seek(TimeSpan.FromSeconds(-1));
        Assert.Equal(TimeSpan.Zero, player.Position);
        player.Seek(TimeSpan.FromMinutes(1));
        Assert.Equal(TimeSpan.FromSeconds(10), player.Position);
        player.Stop();
        Assert.Equal(TimeSpan.Zero, player.Position);
    }
}
