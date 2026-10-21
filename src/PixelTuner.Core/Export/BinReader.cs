using System.Buffers.Binary;
using PixelTuner.Core.Imaging;

namespace PixelTuner.Core.Export;

/// <summary>A parsed "PPOI" file (pixel, fan, video and E-Pic shows). See docs/binary-format.md, section 1.</summary>
public sealed record PpoiFile(int Version, int Height, int Size, IReadOnlyList<PpoiTimeline> Timelines);

public sealed record PpoiTimeline(int Address, Rgb Color, Rgb SelectionColor, uint DurationMs, IReadOnlyList<PpoiEntry> Entries);

/// <summary>One 12-byte record of a timeline: a picture, how fast it plays, the laser mask and the start.</summary>
public sealed record PpoiEntry(int Index, uint ImageAddress, ushort Stretching, ushort Mask, uint StartMs, PpoiImage Image)
{
    public bool LaserOn => (Mask & 1) != 0;
}

/// <summary>An image block. Entries that share a block share this object's address.</summary>
public sealed class PpoiImage(uint address, byte bitsPerChannel, int height, ReadOnlyMemory<byte> pixels)
{
    public uint Address { get; } = address;

    /// <summary>4 (12 bit colour), 8 (24 bit) or 12 (36 bit).</summary>
    public byte BitsPerChannel { get; } = bitsPerChannel;

    public int Height { get; } = height;

    /// <summary>Pixel bytes after the 5-byte block header.</summary>
    public ReadOnlyMemory<byte> Pixels { get; } = pixels;

    /// <summary>Size of the whole block in the file.</summary>
    public int BlockSize => 5 + Pixels.Length;

    /// <summary>Bytes of one channel of one column.</summary>
    public int ChannelBytes => BitsPerChannel switch { 4 => (Height + 1) / 2, 8 => Height, _ => Height * 2 };

    public int Width => Pixels.Length / (ChannelBytes * 3);

    /// <summary>
    /// The encoded value of a channel (0 red, 1 green, 2 blue) of an LED in a column: a byte for 24 bit,
    /// a 12-bit value for 36 bit, a nibble for 12 bit. It is the device's value, after brightness and colour
    /// correction, not the colour in the source picture.
    /// </summary>
    public int Value(int column, int row, int channel)
    {
        int start = (column * 3 + channel) * ChannelBytes;
        var span = Pixels.Span;
        return BitsPerChannel switch
        {
            8 => span[start + row],
            12 => BinaryPrimitives.ReadUInt16LittleEndian(span[(start + row * 2)..]),
            _ => (row % 2 == 0 ? span[start + row / 2] >> 4 : span[start + row / 2]) & 0xF,
        };
    }
}

/// <summary>A parsed "LGHT" file (LightMaster). See docs/binary-format.md, section 3.</summary>
public sealed record LghtFile(int Size, int SamplesAddress, int Slots, int PointSize, uint DurationMs, IReadOnlyList<LghtSample> Samples)
{
    /// <summary>The sample in force at a time: the last stored one that is not later.</summary>
    public LghtSample At(uint timeMs)
    {
        var result = Samples[0];
        foreach (var sample in Samples)
        {
            if (sample.TimeMs > timeMs) break;
            result = sample;
        }
        return result;
    }
}

public sealed record LghtSample(uint TimeMs, byte[] Colors)
{
    /// <summary>The RGB of a slot.</summary>
    public Rgb Slot(int slot) => new(Colors[slot * 3], Colors[slot * 3 + 1], Colors[slot * 3 + 2]);
}

/// <summary>
/// Reads the .bin files back the way a device would. The editor's tests build a show, read it here and
/// compare the field the control was meant to set; the preview plays its pictures from what this returns.
/// </summary>
/// <remarks>
/// The E-Pic 12-bit block is not readable: PixelTuner 2 wrote it broken (see docs/binary-format.md, section 2).
/// </remarks>
public static class BinReader
{
    private const int HeaderSize = 12;
    private const int TimelineHeaderSize = 12;
    private const int EntrySize = 12;
    private const int LghtHeaderSize = 40;

    public static bool IsPpoi(ReadOnlySpan<byte> bin) => bin.Length >= 4 && bin[..4].SequenceEqual("PPOI"u8);

    public static bool IsLght(ReadOnlySpan<byte> bin) => bin.Length >= 4 && bin[..4].SequenceEqual("LGHT"u8);

    /// <exception cref="InvalidDataException">The bytes are not a well-formed PPOI file.</exception>
    public static PpoiFile ReadPpoi(ReadOnlySpan<byte> bin)
    {
        if (!IsPpoi(bin)) throw new InvalidDataException("Not a PPOI file.");
        if (bin.Length < HeaderSize) throw new InvalidDataException("The PPOI header is cut off.");
        int version = bin[4];
        int height = BinaryPrimitives.ReadUInt16LittleEndian(bin[5..]);
        uint size = BinaryPrimitives.ReadUInt32LittleEndian(bin[7..]);
        if (size != bin.Length) throw new InvalidDataException($"The header says {size} bytes, the file has {bin.Length}.");
        int count = bin[11];

        var blocks = new Dictionary<uint, PpoiImage>();
        var timelines = new List<PpoiTimeline>();
        for (int t = 0; t < count; t++)
        {
            int at = Address(bin, HeaderSize + 4 * t, TimelineHeaderSize);
            int entries = BinaryPrimitives.ReadUInt16LittleEndian(bin[at..]);
            var list = new List<PpoiEntry>(entries);
            for (int i = 0; i < entries; i++)
            {
                int e = at + TimelineHeaderSize + EntrySize * i;
                if (e + EntrySize > bin.Length) throw new InvalidDataException("A timeline entry is cut off.");
                uint address = BinaryPrimitives.ReadUInt32LittleEndian(bin[e..]);
                if (!blocks.TryGetValue(address, out var image))
                    blocks[address] = image = ReadImage(bin, address, height);
                list.Add(new PpoiEntry(i, address, BinaryPrimitives.ReadUInt16LittleEndian(bin[(e + 4)..]),
                    BinaryPrimitives.ReadUInt16LittleEndian(bin[(e + 6)..]), BinaryPrimitives.ReadUInt32LittleEndian(bin[(e + 8)..]), image));
            }
            timelines.Add(new PpoiTimeline(at, new Rgb(bin[at + 2], bin[at + 3], bin[at + 4]), new Rgb(bin[at + 5], bin[at + 6], bin[at + 7]),
                BinaryPrimitives.ReadUInt32LittleEndian(bin[(at + 8)..]), list));
        }
        return new PpoiFile(version, height, (int)size, timelines);
    }

    /// <summary>The blocks of a file in address order, each once.</summary>
    public static IEnumerable<PpoiImage> Images(PpoiFile file) =>
        file.Timelines.SelectMany(t => t.Entries).Select(e => e.Image).DistinctBy(i => i.Address).OrderBy(i => i.Address);

    /// <exception cref="InvalidDataException">The bytes are not a well-formed LGHT file.</exception>
    public static LghtFile ReadLght(ReadOnlySpan<byte> bin)
    {
        if (!IsLght(bin)) throw new InvalidDataException("Not a LGHT file.");
        if (bin.Length < LghtHeaderSize) throw new InvalidDataException("The LGHT header is cut off.");
        int size = (int)BinaryPrimitives.ReadUInt32LittleEndian(bin[4..]);
        if (size != bin.Length) throw new InvalidDataException($"The header says {size} bytes, the file has {bin.Length}.");
        int samplesAddress = (int)BinaryPrimitives.ReadUInt32LittleEndian(bin[8..]);
        int slots = bin[12], pointSize = bin[13];
        uint duration = BinaryPrimitives.ReadUInt32LittleEndian(bin[14..]);
        int step = 4 + pointSize * slots;
        if (samplesAddress < LghtHeaderSize || (bin.Length - samplesAddress) % step != 0)
            throw new InvalidDataException("The samples do not fill the file.");

        var samples = new List<LghtSample>();
        for (int at = samplesAddress; at < bin.Length; at += step)
            samples.Add(new LghtSample(BinaryPrimitives.ReadUInt32LittleEndian(bin[at..]), bin.Slice(at + 4, step - 4).ToArray()));
        return new LghtFile(size, samplesAddress, slots, pointSize, duration, samples);
    }

    private static int Address(ReadOnlySpan<byte> bin, int tableAt, int need)
    {
        if (tableAt + 4 > bin.Length) throw new InvalidDataException("The address table is cut off.");
        uint address = BinaryPrimitives.ReadUInt32LittleEndian(bin[tableAt..]);
        if (address + need > bin.Length) throw new InvalidDataException($"Address {address} lies outside the file.");
        return (int)address;
    }

    private static PpoiImage ReadImage(ReadOnlySpan<byte> bin, uint address, int height)
    {
        if (address + 5 > bin.Length) throw new InvalidDataException($"Image address {address} lies outside the file.");
        byte bits = bin[(int)address];
        if (bits is not (4 or 8 or 12)) throw new InvalidDataException($"Image at {address} has {bits} bits per channel.");
        uint last = BinaryPrimitives.ReadUInt32LittleEndian(bin[((int)address + 1)..]);
        if (last < address + 4 || last >= bin.Length) throw new InvalidDataException($"The image at {address} ends at {last}, outside the file.");
        var pixels = bin[((int)address + 5)..((int)last + 1)].ToArray();
        var image = new PpoiImage(address, bits, height, pixels);
        if (pixels.Length % (image.ChannelBytes * 3) != 0)
            throw new InvalidDataException($"The image at {address} is not a whole number of columns of height {height}.");
        return image;
    }
}
