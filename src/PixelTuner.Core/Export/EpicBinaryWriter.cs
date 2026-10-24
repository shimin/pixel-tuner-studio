using System.Buffers.Binary;
using System.Text;
using PixelTuner.Core.Imaging;

namespace PixelTuner.Core.Export;

/// <summary>One timeline of an E-Pic poi: a gap-free run of colour gradients from time 0.</summary>
public sealed class EpicExportTimeline
{
    /// <summary>Menu colour of the timeline, written twice (normal and selected), not scaled down.</summary>
    public Rgb Color { get; init; } = new(0, 0, 0x69);

    public List<EpicExportItem> Items { get; } = [];
}

/// <summary>One gradient: a row of colours the poi plays one after another.</summary>
public sealed class EpicExportItem
{
    /// <summary>
    /// Identifies the gradient so that it is stored once. Items with equal keys and brightness
    /// share their data. Null marks a gap filler: all gap fillers share the first one's data.
    /// </summary>
    public string? ImageKey { get; init; }

    /// <summary>The colours, one per step: the top row of the gradient picture.</summary>
    public required Rgb[] Colors { get; init; }

    public uint StartMs { get; init; }
    public uint DurationMs { get; init; }

    /// <summary>Stretching as the user set it; the .bin stores it multiplied by 5.</summary>
    public ushort Stretching { get; init; } = 1;

    public byte Brightness { get; init; } = 100;
    public bool LaserOn { get; init; }
}

/// <summary>
/// Writes the .bin of the E-Pic poi: the "PPOI" version 1 layout of pixel devices with a
/// height of 1 and one colour per column. A port of PixelTuner 2 (Communication/Gradient),
/// byte for byte, because the poi in the field read exactly this.
/// </summary>
/// <remarks>
/// Kept as PixelTuner 2 had it:
/// <list type="bullet">
/// <item>stretching is stored multiplied by 5, the laser bit is 1;</item>
/// <item>24-bit colours are R, G, B of each column with brightness applied and no gamma;</item>
/// </list>
/// Two PixelTuner 2 bugs are fixed (Ivan, 2026-10-24): its 12-bit images were broken (the end
/// address overwrote the bits-per-pixel byte, an odd last nibble was dropped and the next image
/// was placed as if this one took 3 bytes per column), and its 36-bit blue table had 255 entries,
/// so full blue failed. 12-bit images now have the same layout as 24- and 36-bit ones.
/// </remarks>
public static class EpicBinaryWriter
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("PPOI");
    private const byte FormatVersion = 1;
    private const int HeaderSize = 4 + 1 + 2 + 4 + 1;
    private const int TimelineHeaderSize = 2 + 3 + 3 + 4;
    private const int EntrySize = 4 + 2 + 2 + 4;

    public static byte[] Write(IReadOnlyList<EpicExportTimeline> timelines, ColorDepth depth = ColorDepth.Bit24)
    {
        var lines = timelines.Where(t => t.Items.Count > 0).ToList();
        if (lines.Count > byte.MaxValue)
            throw new ArgumentException($"A .bin holds at most {byte.MaxValue} timelines.", nameof(timelines));

        // Distinct gradients in order of first use. PixelTuner 2 kept them in a dictionary keyed by
        // "path_brightness", with one random key for all gap fillers.
        const string gapKey = "\0gap";
        var images = new List<EpicExportItem>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in lines.SelectMany(l => l.Items))
        {
            string key = item.ImageKey is null ? gapKey : $"{item.ImageKey}_{item.Brightness}";
            if (index.ContainsKey(key)) continue;
            index[key] = images.Count;
            images.Add(item);
        }

        long address = HeaderSize + 4L * lines.Count
            + lines.Sum(l => TimelineHeaderSize + (long)EntrySize * l.Items.Count);
        var imageAddress = new uint[images.Count];
        var blocks = new List<byte[]>(images.Count);
        for (int i = 0; i < images.Count; i++)
        {
            imageAddress[i] = checked((uint)address);
            var colors = images[i].Colors.Select(c => Scale(c, images[i].Brightness)).ToArray();
            blocks.Add(depth switch
            {
                ColorDepth.Bit24 => Bit24Image(colors, imageAddress[i]),
                ColorDepth.Bit36 => Bit36Image(colors, imageAddress[i]),
                ColorDepth.Bit12 => Bit12Image(colors, imageAddress[i]),
                _ => throw new ArgumentOutOfRangeException(nameof(depth)),
            });
            address += blocks[i].Length;
        }

        var output = new List<byte>();
        output.AddRange(Magic);
        output.Add(FormatVersion);
        output.AddRange([1, 0]);                      // height: one LED row
        output.AddRange([0, 0, 0, 0]);                // total size, filled in at the end
        output.Add((byte)lines.Count);

        long lineAddress = HeaderSize + 4L * lines.Count;
        foreach (var line in lines)
        {
            AddUInt32(output, checked((uint)lineAddress));
            lineAddress += TimelineHeaderSize + EntrySize * line.Items.Count;
        }

        foreach (var line in lines)
        {
            AddUInt16(output, checked((ushort)line.Items.Count));
            output.AddRange([line.Color.R, line.Color.G, line.Color.B, line.Color.R, line.Color.G, line.Color.B]);
            uint duration = 0;
            foreach (var item in line.Items) duration += item.DurationMs;
            AddUInt32(output, duration);
            foreach (var item in line.Items)
            {
                string key = item.ImageKey is null ? gapKey : $"{item.ImageKey}_{item.Brightness}";
                AddUInt32(output, imageAddress[index[key]]);
                AddUInt16(output, unchecked((ushort)(item.Stretching * 5)));
                AddUInt16(output, item.LaserOn ? (ushort)1 : (ushort)0);
                AddUInt32(output, item.StartMs);
            }
        }

        foreach (var block in blocks) output.AddRange(block);

        var result = output.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(7), checked((uint)result.Length));
        return result;
    }

    /// <summary>Brightness as PixelTuner 2 applied it: a float factor, truncated.</summary>
    private static Rgb Scale(Rgb c, byte brightness)
    {
        float m = (float)brightness / 100;
        return new Rgb((byte)(c.R * m), (byte)(c.G * m), (byte)(c.B * m));
    }

    /// <summary>8 bits per pixel, the address of the last byte, then R, G, B of each column.</summary>
    private static byte[] Bit24Image(Rgb[] colors, uint start)
    {
        var block = new byte[5 + colors.Length * 3];
        block[0] = 8;
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(1), (uint)(start + block.Length - 1));
        for (int i = 0; i < colors.Length; i++)
        {
            block[5 + 3 * i] = colors[i].R;
            block[6 + 3 * i] = colors[i].G;
            block[7 + 3 * i] = colors[i].B;
        }
        return block;
    }

    /// <summary>12 bits per pixel through the E-Pic calibration tables, two bytes per value.</summary>
    private static byte[] Bit36Image(Rgb[] colors, uint start)
    {
        var block = new byte[5 + colors.Length * 6];
        block[0] = 12;
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(1), (uint)(start + block.Length - 1));
        var r = GradientColorProfile.R;
        var g = GradientColorProfile.G;
        var b = GradientColorProfile.B;
        for (int i = 0; i < colors.Length; i++)
        {
            var span = block.AsSpan(5 + 6 * i);
            BinaryPrimitives.WriteUInt16LittleEndian(span, r[colors[i].R]);
            BinaryPrimitives.WriteUInt16LittleEndian(span[2..], g[colors[i].G]);
            BinaryPrimitives.WriteUInt16LittleEndian(span[4..], b[colors[i].B]);
        }
        return block;
    }

    /// <summary>
    /// 4 bits per value, packed R, G, B, R, G, B, ... two to a byte, high nibble first; an odd
    /// last value takes the high nibble of the last byte.
    /// </summary>
    private static byte[] Bit12Image(Rgb[] colors, uint start)
    {
        int values = colors.Length * 3;
        var block = new byte[5 + (values + 1) / 2];
        block[0] = 4;
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(1), (uint)(start + block.Length - 1));
        int n = 0;
        foreach (var c in colors)
        {
            foreach (byte channel in (ReadOnlySpan<byte>)[c.R, c.G, c.B])
            {
                int v = 15 * channel / 255;
                block[5 + n / 2] |= (byte)(n % 2 == 0 ? v << 4 : v);
                n++;
            }
        }
        return block;
    }

    private static void AddUInt16(List<byte> output, ushort value)
    {
        output.Add((byte)value);
        output.Add((byte)(value >> 8));
    }

    private static void AddUInt32(List<byte> output, uint value)
    {
        for (int i = 0; i < 4; i++) output.Add((byte)(value >> (8 * i)));
    }
}
