namespace PixelTuner.Audio.Vlc.Tests;

public class VlcAudioDecoderTests
{
    [Fact]
    public async Task A_wav_becomes_a_waveform_of_the_right_length_and_loudness()
    {
        if (VlcFixture.Get() is not { } library) return;
        var decoder = new VlcAudioDecoder(library);

        var wave = await decoder.ReadWaveformAsync(new MemoryStream(VlcFixture.Wav(2, amplitude: 0.5)), "tone.wav");

        Assert.InRange(wave.Duration.TotalSeconds, 1.9, 2.05);   // the transcoder may drop a few milliseconds at the start
        Assert.InRange(wave.Count, 380, 410);                       // 200 points a second
        var (min, max) = wave.Range(TimeSpan.Zero, TimeSpan.FromSeconds(2));
        Assert.InRange(max, 0.4, 0.6);
        Assert.InRange(min, -0.6, -0.4);
    }

    [Fact]
    public async Task The_two_channels_of_a_stereo_sound_are_kept_apart_and_a_mono_sound_is_in_both()
    {
        if (VlcFixture.Get() is not { } library) return;
        var decoder = new VlcAudioDecoder(library);

        var stereo = await decoder.ReadWaveformAsync(new MemoryStream(VlcFixture.Wav(2, amplitude: 0.8, rightAmplitude: 0.2)), "stereo.wav");
        var (left, right) = stereo.Peaks(TimeSpan.Zero, stereo.Duration);
        Assert.InRange(left, 0.7, 0.9);
        Assert.InRange(right, 0.15, 0.3);

        var mono = await decoder.ReadWaveformAsync(new MemoryStream(VlcFixture.Wav(2, amplitude: 0.5)), "mono.wav");
        var (monoLeft, monoRight) = mono.Peaks(TimeSpan.Zero, mono.Duration);
        Assert.InRange(monoLeft, 0.4, 0.6);
        Assert.InRange(monoRight, 0.4, 0.6);
    }

    [Fact]
    public async Task Silence_then_sound_is_quiet_first_and_loud_after()
    {
        if (VlcFixture.Get() is not { } library) return;
        var decoder = new VlcAudioDecoder(library);

        var wave = await decoder.ReadWaveformAsync(new MemoryStream(VlcFixture.Wav(4, amplitude: 0.8, silence: 2)), "late.wav");

        var (_, quietMax) = wave.Range(TimeSpan.FromSeconds(0.2), TimeSpan.FromSeconds(1.8));
        var (_, loudMax) = wave.Range(TimeSpan.FromSeconds(2.2), TimeSpan.FromSeconds(3.8));
        Assert.True(quietMax < 0.05, $"expected silence, got {quietMax}");
        Assert.True(loudMax > 0.6, $"expected sound, got {loudMax}");
    }

    [Fact]
    public async Task An_mp3_is_read_too()
    {
        if (VlcFixture.Get() is not { } library) return;
        var decoder = new VlcAudioDecoder(library);
        await using var file = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tone.mp3"));

        var wave = await decoder.ReadWaveformAsync(file, "tone.mp3");

        Assert.InRange(wave.Duration.TotalSeconds, 0.9, 1.2);
        Assert.True(wave.Range(TimeSpan.Zero, wave.Duration).Max > 0.05);   // ffmpeg's sine is quiet (amplitude 1/8)
    }

    [Fact]
    public async Task Something_that_is_not_a_sound_is_refused()
    {
        if (VlcFixture.Get() is not { } library) return;
        var decoder = new VlcAudioDecoder(library);

        await Assert.ThrowsAsync<AudioFormatException>(() =>
            decoder.ReadWaveformAsync(new MemoryStream("this is not audio at all"u8.ToArray()), "notes.mp3"));
    }

    [Fact]
    public async Task A_cancelled_read_stops()
    {
        if (VlcFixture.Get() is not { } library) return;
        var decoder = new VlcAudioDecoder(library);
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            decoder.ReadWaveformAsync(new MemoryStream(VlcFixture.Wav(2)), "tone.wav", cancel: cancel.Token));
    }

    [Fact]
    public async Task A_long_sound_is_decoded_much_faster_than_it_plays()
    {
        if (VlcFixture.Get() is not { } library) return;
        var decoder = new VlcAudioDecoder(library);
        var started = DateTime.UtcNow;

        var wave = await decoder.ReadWaveformAsync(new MemoryStream(VlcFixture.Wav(120)), "long.wav");

        Assert.InRange(wave.Duration.TotalSeconds, 119, 121);
        Assert.True((DateTime.UtcNow - started).TotalSeconds < 30, "decoding two minutes should not take real time");
    }
}
