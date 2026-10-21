using System.Buffers.Binary;
using LibVLCSharp.Shared;

namespace PixelTuner.Audio.Vlc;

/// <summary>
/// Reads a soundtrack into a <see cref="Waveform"/>. LibVLC decodes any format it plays to a temporary
/// mono WAV, faster than real time, and the samples are then folded into the waveform. The same engine
/// plays the sound, so what the timelines are drawn over is what is heard.
/// </summary>
public sealed class VlcAudioDecoder : IAudioDecoder
{
    private const int SampleRate = 22050;   // plenty for a waveform, and a whole number of frames per point at 200 points a second

    private readonly VlcLibrary _library;

    public VlcAudioDecoder(VlcLibrary library) => _library = library;

    public async Task<Waveform> ReadWaveformAsync(Stream source, string fileName, int pointsPerSecond = Waveform.DefaultPointsPerSecond,
        CancellationToken cancel = default)
    {
        string wav = Path.Combine(Path.GetTempPath(), $"pixeltuner-{Guid.NewGuid():N}.wav");
        try
        {
            var data = new MemoryStream();
            await source.CopyToAsync(data, cancel);
            data.Position = 0;
            await TranscodeAsync(data, fileName, wav, cancel);
            return ReadWav(wav, fileName, pointsPerSecond);
        }
        finally
        {
            try { File.Delete(wav); } catch (IOException) { }
        }
    }

    private async Task TranscodeAsync(MemoryStream data, string fileName, string wavPath, CancellationToken cancel)
    {
        using var media = new Media(_library.Engine, new StreamMediaInput(data));
        // The chain quotes the path: a Windows path has a colon and backslashes.
        string quoted = "\"" + wavPath.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        media.AddOption($":sout=#transcode{{acodec=s16l,channels=1,samplerate={SampleRate}}}:std{{access=file,mux=wav,dst={quoted}}}");
        media.AddOption(":no-sout-all");
        media.AddOption(":sout-keep");

        using var player = new MediaPlayer(media);
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        player.EndReached += (_, _) => ended.TrySetResult();
        player.EncounteredError += (_, _) => ended.TrySetException(new AudioFormatException($"“{fileName}” is not a sound that can be read."));
        player.Stopped += (_, _) => stopped.TrySetResult();

        using var registration = cancel.Register(() => ended.TrySetCanceled(cancel));
        if (!player.Play()) throw new AudioFormatException($"“{fileName}” is not a sound that can be read.");

        try
        {
            await ended.Task;
        }
        finally
        {
            // The file is complete only once the player has stopped (from outside LibVLC's own thread).
            await Task.Run(() => player.Stop());
            await Task.WhenAny(stopped.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        }
    }

    private static Waveform ReadWav(string path, string fileName, int pointsPerSecond)
    {
        using var file = File.OpenRead(path);
        Span<byte> header = stackalloc byte[12];
        if (file.Read(header) < 12 || !header[..4].SequenceEqual("RIFF"u8) || !header.Slice(8, 4).SequenceEqual("WAVE"u8))
            throw new AudioFormatException($"“{fileName}” is not a sound that can be read.");

        int channels = 1, rate = SampleRate, bits = 16;
        Span<byte> chunk = stackalloc byte[8];
        while (file.Read(chunk) == 8)
        {
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(chunk[4..]);
            if (chunk[..4].SequenceEqual("fmt "u8))
            {
                var fmt = new byte[Math.Min(size, 64u)];
                file.ReadExactly(fmt);
                channels = BinaryPrimitives.ReadUInt16LittleEndian(fmt.AsSpan(2));
                rate = (int)BinaryPrimitives.ReadUInt32LittleEndian(fmt.AsSpan(4));
                bits = BinaryPrimitives.ReadUInt16LittleEndian(fmt.AsSpan(14));
                file.Seek(size - fmt.Length + (size & 1), SeekOrigin.Current);
            }
            else if (chunk[..4].SequenceEqual("data"u8))
            {
                if (bits != 16 || channels < 1) throw new AudioFormatException($"“{fileName}” decoded to an unexpected format.");
                // The size field of a streamed WAV may be 0 or 0xFFFFFFFF: read to the end of the file.
                return FoldSamples(file, channels, rate, pointsPerSecond, fileName);
            }
            else
            {
                file.Seek(size + (size & 1), SeekOrigin.Current);
            }
        }
        throw new AudioFormatException($"“{fileName}” has no sound in it.");
    }

    private static Waveform FoldSamples(Stream file, int channels, int rate, int pointsPerSecond, string fileName)
    {
        var builder = new WaveformBuilder(rate, channels, pointsPerSecond);
        var bytes = new byte[64 * 1024];
        var samples = new float[bytes.Length / 2];
        int carry = 0;
        long total = 0;
        int read;
        while ((read = file.Read(bytes, carry, bytes.Length - carry)) > 0)
        {
            int available = carry + read;
            int count = available / 2;
            for (int i = 0; i < count; i++) samples[i] = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(i * 2)) / 32768f;
            builder.Add(samples.AsSpan(0, count));
            total += count;
            carry = available - count * 2;
            if (carry > 0) bytes[0] = bytes[count * 2];
        }
        if (total == 0) throw new AudioFormatException($"“{fileName}” has no sound in it.");
        return builder.Build();
    }
}
