using System.Buffers.Binary;

namespace PixelTuner.Audio.Vlc.Tests;

internal static class VlcFixture
{
    private static readonly Lazy<VlcLibrary?> Library = new(() => VlcLibrary.TryCreate("--aout=dummy"));

    /// <summary>
    /// The LibVLC to test with, or null when there is none on this machine and the test should stop quietly.
    /// CI sets PIXELTUNER_REQUIRE_VLC=1 so that a missing LibVLC there is a failure and not a silent pass.
    /// </summary>
    public static VlcLibrary? Get()
    {
        var library = Library.Value;
        if (library is null && Environment.GetEnvironmentVariable("PIXELTUNER_REQUIRE_VLC") == "1")
            throw new InvalidOperationException("LibVLC could not be loaded, and PIXELTUNER_REQUIRE_VLC is set.");
        return library;
    }

    /// <summary>A 16-bit mono WAV: silence for <paramref name="silence"/> seconds, then a sine of the given amplitude.</summary>
    public static byte[] Wav(double seconds, double amplitude = 0.5, double silence = 0, int rate = 44100, double? rightAmplitude = null)
    {
        int channels = rightAmplitude is null ? 1 : 2;
        int frames = (int)(seconds * rate);
        var data = new byte[44 + frames * 2 * channels];
        "RIFF"u8.CopyTo(data);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), data.Length - 8);
        "WAVEfmt "u8.CopyTo(data.AsSpan(8));
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(20), 1);       // PCM
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(22), (short)channels);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(24), rate);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(28), rate * 2 * channels);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(32), (short)(2 * channels));
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(34), 16);
        "data"u8.CopyTo(data.AsSpan(36));
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(40), frames * 2 * channels);
        for (int i = 0; i < frames; i++)
        {
            double t = (double)i / rate;
            double tone = t < silence ? 0 : Math.Sin(2 * Math.PI * 440 * t);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(44 + i * 2 * channels), (short)(amplitude * tone * 32767));
            if (rightAmplitude is { } right) BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(46 + i * 4), (short)(right * tone * 32767));
        }
        return data;
    }

    public static async Task<bool> WaitAsync(Func<bool> condition, double seconds = 5)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < until)
        {
            if (condition()) return true;
            await Task.Delay(20);
        }
        return condition();
    }
}
