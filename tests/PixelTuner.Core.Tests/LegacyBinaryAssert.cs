using System.Buffers.Binary;

namespace PixelTuner.Core.Tests;

/// <summary>Compares an exported .bin with the one PixelTuner 2 wrote.</summary>
internal static class LegacyBinaryAssert
{
    /// <summary>
    /// Byte for byte, except inside image blocks listed in <paramref name="jpegImages"/>:
    /// there each byte may differ by one level, because JPEG decoders round differently.
    /// </summary>
    public static void Equal(byte[] expected, byte[] actual, IReadOnlySet<uint>? jpegImages = null)
    {
        Assert.Equal(expected.Length, actual.Length);
        if (expected.AsSpan().SequenceEqual(actual)) return;

        var ranges = ImageRanges(expected).Where(r => jpegImages?.Contains(r.Start) == true).ToList();
        for (int i = 0; i < expected.Length; i++)
        {
            if (expected[i] == actual[i]) continue;
            bool inJpeg = ranges.Any(r => i > r.Start + 4 && i < r.End);
            Assert.True(inJpeg && Math.Abs(expected[i] - actual[i]) <= 1,
                $"Byte {i} (0x{i:X}) differs: PixelTuner 2 wrote {expected[i]}, got {actual[i]}.");
        }
    }

    /// <summary>Start address of every image used by a timeline item, with the item's index.</summary>
    public static IEnumerable<(int Timeline, int Item, uint Address)> ImageEntries(byte[] bin)
    {
        int count = bin[11];
        for (int t = 0; t < count; t++)
        {
            int line = (int)BinaryPrimitives.ReadUInt32LittleEndian(bin.AsSpan(12 + 4 * t));
            int items = BinaryPrimitives.ReadUInt16LittleEndian(bin.AsSpan(line));
            for (int i = 0; i < items; i++)
                yield return (t, i, BinaryPrimitives.ReadUInt32LittleEndian(bin.AsSpan(line + 12 + 12 * i)));
        }
    }

    private static IEnumerable<(uint Start, uint End)> ImageRanges(byte[] bin) =>
        ImageEntries(bin).Select(e => e.Address).Distinct()
            .Select(a => (a, BinaryPrimitives.ReadUInt32LittleEndian(bin.AsSpan((int)a + 1))));
}
