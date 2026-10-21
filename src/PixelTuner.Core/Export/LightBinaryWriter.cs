using System.Buffers.Binary;
using System.Text;
using PixelTuner.Core.Imaging;

namespace PixelTuner.Core.Export;

/// <summary>What a LightMaster item does over its time.</summary>
public enum LightEffect
{
    /// <summary>One colour (<see cref="LightExportItem.ColorFirst"/>).</summary>
    Solid,
    LinearTransition,
    SineWave,
    SquareWave,
    SawToothWave,
}

/// <summary>One light channel (a submodule of the LightMaster).</summary>
public sealed class LightExportTimeline
{
    /// <summary>A monochrome channel uses only the red value and shares a sample slot with two others.</summary>
    public bool IsMonochrome { get; init; }

    /// <summary>Gap-free items from time 0.</summary>
    public List<LightExportItem> Items { get; } = [];
}

public sealed class LightExportItem
{
    public LightEffect Effect { get; init; }
    public uint StartMs { get; init; }
    public uint EndMs { get; init; }
    public Rgb ColorFirst { get; init; }
    public Rgb ColorLast { get; init; }

    /// <summary>Effect periods in milliseconds, as PixelTuner 2's T1 and T2.</summary>
    public int T1 { get; init; }
    public int T2 { get; init; }
}

/// <summary>
/// Writes the "LGHT" .bin of the LightMaster: the colour of every channel sampled at a fixed
/// rate, storing only samples that differ from the previous one. A port of PixelTuner 2
/// (Communication/Binarize), byte for byte.
/// </summary>
/// <remarks>
/// Layout, little endian:
/// <code>
/// "LGHT" u32 total size  u32 samples address = 40  u8 channel count  u8 point size = 3
/// u32 duration ms, zeros up to the samples address
/// per stored sample: u32 time ms, then RGB of each channel (three monochrome channels
///                    share one RGB slot)
/// </code>
/// Kept as PixelTuner 2 had it: a duration that is not a multiple of the rate is turned into
/// duration / rate * (rate + 1) (in floating point); every item is sampled from its own start
/// in rate steps, so items whose length is not a multiple of the rate shift the ones after them;
/// the effect formulas below, including the Saw-tooth one, which uses an arctangent as a slope.
/// </remarks>
public static class LightBinaryWriter
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("LGHT");
    private const int PointSize = 3;
    private const int SamplesAddress = 40;

    /// <param name="rate">Time between samples in milliseconds: PixelTuner 2's "discreteness" setting (5 by default).</param>
    public static byte[] Write(IReadOnlyList<LightExportTimeline> timelines, int rate = 5)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rate);
        var lines = timelines.Where(t => t.Items.Count > 0).ToList();

        var output = new List<byte>();
        output.AddRange(Magic);
        output.AddRange([0, 0, 0, 0]);
        AddUInt32(output, SamplesAddress);
        output.Add(ChannelCount(lines));
        output.Add(PointSize);

        double duration = lines.Count == 0 ? 0 : lines.Max(l => l.Items[^1].EndMs);
        if (duration % rate != 0) duration = duration / rate * (rate + 1);
        int samplesCount = (int)(duration / rate) + 1;
        // PixelTuner 2 counted samples in a UInt16 and never finished beyond that.
        if (samplesCount > ushort.MaxValue)
            throw new NotSupportedException("The show is too long for the LightMaster at this sampling rate.");

        var channels = lines.Select(l => Channel(l, samplesCount, rate)).ToList();
        AddUInt32(output, (uint)duration);
        while (output.Count < SamplesAddress) output.Add(0);

        List<byte>? previous = null;
        for (int i = 0; i < samplesCount; i++)
        {
            var sample = new List<byte>();
            for (int j = 0; j < channels.Count; j++)
            {
                if (lines[j].IsMonochrome) sample.Add(channels[j][i * PointSize]);
                else
                {
                    while (sample.Count % PointSize != 0) sample.Add(0);
                    sample.AddRange(channels[j].GetRange(i * PointSize, PointSize));
                }
            }
            while (sample.Count % PointSize != 0) sample.Add(0);

            if (previous is null || !previous.SequenceEqual(sample) || i == samplesCount - 1)
            {
                AddUInt32(output, (uint)(i * rate));
                output.AddRange(sample);
            }
            previous = sample;
        }

        var result = output.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(4), result.Length);
        return result;
    }

    /// <summary>RGB slots: one per colour channel, one per run of up to three monochrome channels.</summary>
    private static byte ChannelCount(List<LightExportTimeline> lines)
    {
        byte result = 0;
        int monochrome = 0;
        foreach (var line in lines)
        {
            if (line.IsMonochrome)
            {
                monochrome++;
                if (monochrome % 3 == 0) result++;
            }
            else
            {
                if (monochrome % 3 != 0)
                {
                    result++;
                    monochrome = 0;
                }
                result++;
            }
        }
        if (monochrome % 3 != 0) result++;
        return result;
    }

    /// <summary>RGB triples of a channel: every item sampled in turn, then black to the end of the show.</summary>
    private static List<byte> Channel(LightExportTimeline line, int samplesCount, int rate)
    {
        var result = new List<byte>();
        foreach (var item in line.Items)
        {
            for (long t = item.StartMs; t < item.EndMs; t += rate)
            {
                var c = Sample(item, t - item.StartMs);
                result.Add(c.R);
                result.Add(c.G);
                result.Add(c.B);
            }
        }
        for (int i = 0; i < samplesCount; i++) result.AddRange([0, 0, 0]);
        return result;
    }

    /// <summary>Colour of an item <paramref name="t"/> milliseconds after its start, as PixelTuner 2's LightEffectFile.</summary>
    internal static Rgb Sample(LightExportItem item, double t)
    {
        var first = item.ColorFirst;
        var last = item.ColorLast;
        int dR = last.R - first.R, dG = last.G - first.G, dB = last.B - first.B;
        int t1 = item.T1, t2 = item.T2;
        switch (item.Effect)
        {
            case LightEffect.Solid:
                return first;

            case LightEffect.LinearTransition:
            {
                double length = (double)item.EndMs - item.StartMs;
                return new Rgb(
                    unchecked((byte)(first.R + (int)(dR * t / length))),
                    unchecked((byte)(first.G + (int)(dG * t / length))),
                    unchecked((byte)(first.B + (int)(dB * t / length))));
            }

            case LightEffect.SineWave:
            {
                double s = Math.Sin(2 * Math.PI * t / t1 - Math.PI / 2);
                return new Rgb(
                    LegacyByte(Math.Abs(dR / 2 * s + dR / 2 + first.R)),
                    LegacyByte(Math.Abs(dG / 2 * s + dG / 2 + first.G)),
                    LegacyByte(Math.Abs(dB / 2 * s + dB / 2 + first.B)));
            }

            case LightEffect.SquareWave:
            {
                if (t2 == 0) return first;
                if (t1 == 0) return last;
                return t % (t1 + t2) <= t1 ? first : last;
            }

            case LightEffect.SawToothWave:
            {
                double m = t % (t1 + t2);
                return new Rgb(Saw(m, t1, t2, dR, first.R, last.R), Saw(m, t1, t2, dG, first.G, last.G),
                    Saw(m, t1, t2, dB, first.B, last.B));
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(item));
        }
    }

    private static byte Saw(double m, int t1, int t2, int d, byte first, byte last)
    {
        double x = m > t1
            ? Math.Atan((double)-Math.Abs(d) / t2) * (m - t1) + last
            : Math.Atan((double)Math.Abs(d) / t1) * m + first;
        x = x > 255 ? 255 - (x - 255) : Math.Abs(x);
        return LegacyByte(x);
    }

    /// <summary>
    /// The formulas above always land in 0..255 (a period of 0 gives NaN), where a cast to byte
    /// truncates the same on .NET Framework and today; NaN became 0 then and does now.
    /// </summary>
    private static byte LegacyByte(double value) => double.IsNaN(value) ? (byte)0 : (byte)value;

    private static void AddUInt32(List<byte> output, uint value)
    {
        for (int i = 0; i < 4; i++) output.Add((byte)(value >> (8 * i)));
    }
}
