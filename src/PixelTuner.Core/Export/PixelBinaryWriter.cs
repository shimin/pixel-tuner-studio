using System.Buffers.Binary;
using System.Text;
using PixelTuner.Core.Imaging;

namespace PixelTuner.Core.Export;

/// <summary>
/// Writes the "PPOI" version 1 .bin that pixel devices play from their memory.
/// A port of PixelTuner 2 (Communication/Pixelate): the output must stay byte for byte
/// the same, because devices in the field read exactly this layout.
/// </summary>
/// <remarks>
/// Layout, all numbers little endian:
/// <code>
/// "PPOI" u8 version=1  u16 height  u32 total size  u8 timeline count
/// u32 address of each timeline
/// per timeline: u16 image count, RGB colour, RGB selection colour, u32 duration ms,
///               per image: u32 image address, u16 stretching, u16 mask, u32 start ms
/// per distinct image: u8 bits per pixel, u32 address of its last byte, then per column
///               all R values, all G values, all B values (top LED first)
/// </code>
/// </remarks>
public static class PixelBinaryWriter
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("PPOI");
    private const byte FormatVersion = 1;
    private const int HeaderSize = 4 + 1 + 2 + 4 + 1;
    private const int TimelineHeaderSize = 2 + 3 + 3 + 4;
    private const int ImageEntrySize = 4 + 2 + 2 + 4;
    private const int ImageHeaderSize = 1 + 4;

    public static byte[] Write(IReadOnlyList<PixelExportTimeline> timelines, ushort height, ColorDepth depth = ColorDepth.Bit24)
    {
        var lines = timelines.Where(t => t.Items.Count > 0).ToList();
        if (lines.Count > byte.MaxValue)
            throw new ArgumentException($"A .bin holds at most {byte.MaxValue} timelines.", nameof(timelines));

        // Distinct images in order of first use; PixelTuner 2 stored them in that order.
        var images = new List<byte[]>();
        var imageIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var itemImage = new Dictionary<PixelExportItem, int>(ReferenceEqualityComparer.Instance);
        foreach (var line in lines)
        {
            foreach (var item in line.Items)
            {
                string? key = item.ImageKey is null ? null : $"{item.ImageKey}_{item.Brightness}_{item.FlipVertical}";
                if (key is not null && imageIndex.TryGetValue(key, out int existing))
                {
                    itemImage[item] = existing;
                    continue;
                }
                var image = item.FlipVertical ? item.Image.FlippedVertically() : item.Image;
                images.Add(EncodeImage(image, height, depth, item.Brightness));
                itemImage[item] = images.Count - 1;
                if (key is not null) imageIndex[key] = images.Count - 1;
            }
        }

        long timelinesStart = HeaderSize + 4L * lines.Count;
        long imagesStart = timelinesStart + lines.Sum(l => TimelineHeaderSize + (long)ImageEntrySize * l.Items.Count);
        var imageAddress = new uint[images.Count];
        long address = imagesStart;
        for (int i = 0; i < images.Count; i++)
        {
            imageAddress[i] = checked((uint)address);
            // Address of the image's last byte, written right after the bits-per-pixel byte.
            BinaryPrimitives.WriteUInt32LittleEndian(images[i].AsSpan(1), checked((uint)(address + images[i].Length - 1)));
            address += images[i].Length;
        }
        uint totalSize = checked((uint)address);

        var output = new byte[totalSize];
        var span = output.AsSpan();
        Magic.CopyTo(span);
        span[4] = FormatVersion;
        BinaryPrimitives.WriteUInt16LittleEndian(span[5..], height);
        BinaryPrimitives.WriteUInt32LittleEndian(span[7..], totalSize);
        span[11] = (byte)lines.Count;

        int pos = HeaderSize;
        long lineAddress = timelinesStart;
        foreach (var line in lines)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], (uint)lineAddress);
            pos += 4;
            lineAddress += TimelineHeaderSize + ImageEntrySize * line.Items.Count;
        }

        foreach (var line in lines)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], checked((ushort)line.Items.Count));
            span[pos + 2] = line.Color.R;
            span[pos + 3] = line.Color.G;
            span[pos + 4] = line.Color.B;
            span[pos + 5] = line.SelectionColor.R;
            span[pos + 6] = line.SelectionColor.G;
            span[pos + 7] = line.SelectionColor.B;
            uint duration = 0;
            foreach (var item in line.Items) duration += item.DurationMs;
            BinaryPrimitives.WriteUInt32LittleEndian(span[(pos + 8)..], duration);
            pos += TimelineHeaderSize;

            foreach (var item in line.Items)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], imageAddress[itemImage[item]]);
                BinaryPrimitives.WriteUInt16LittleEndian(span[(pos + 4)..], item.Stretching);
                BinaryPrimitives.WriteUInt16LittleEndian(span[(pos + 6)..], item.LaserOn ? item.LaserMask : (ushort)0);
                BinaryPrimitives.WriteUInt32LittleEndian(span[(pos + 8)..], item.StartMs);
                pos += ImageEntrySize;
            }
        }

        foreach (var image in images)
        {
            image.CopyTo(span[pos..]);
            pos += image.Length;
        }
        return output;
    }

    /// <summary>
    /// One image block: bits per pixel, a 4-byte placeholder for the end address, then the columns.
    /// </summary>
    internal static byte[] EncodeImage(PixelImage image, ushort height, ColorDepth depth, byte brightness)
    {
        var bytes = new List<byte>(ImageHeaderSize + image.Width * height * 3);
        bytes.Add(depth switch
        {
            ColorDepth.Bit12 => 4,
            ColorDepth.Bit24 => 8,
            ColorDepth.Bit36 => 12,
            _ => throw new ArgumentOutOfRangeException(nameof(depth)),
        });
        bytes.AddRange([0, 0, 0, 0]);

        var column = new Rgb[height];
        var pixel = new PixelColor(height, brightness);
        for (int x = 0; x < image.Width; x++)
        {
            ReadColumn(image, height, x, column);
            switch (depth)
            {
                case ColorDepth.Bit24:
                    for (int channel = 0; channel < 3; channel++)
                        foreach (var c in column) bytes.Add(pixel.To8Bit(c, channel));
                    break;
                case ColorDepth.Bit36:
                    for (int channel = 0; channel < 3; channel++)
                        foreach (var c in column)
                        {
                            ushort value = pixel.To12Bit(c, channel);
                            bytes.Add((byte)value);
                            bytes.Add((byte)(value >> 8));
                        }
                    break;
                case ColorDepth.Bit12:
                    for (int channel = 0; channel < 3; channel++)
                    {
                        // Two 4-bit values per byte, the first in the high nibble.
                        for (int row = 0; row < column.Length; row += 2)
                        {
                            int high = pixel.To4Bit(column[row], channel) << 4;
                            int low = row + 1 < column.Length ? pixel.To4Bit(column[row + 1], channel) : 0;
                            bytes.Add((byte)(high + low));
                        }
                    }
                    break;
            }
        }
        return [.. bytes];
    }

    /// <summary>
    /// The image is aligned to the bottom LED: when it is shorter than the device,
    /// the top rows are black; when it is taller, its top rows are cut off.
    /// </summary>
    private static void ReadColumn(PixelImage image, ushort height, int x, Rgb[] column)
    {
        int offset = height - image.Height;
        for (int row = 0; row < height; row++)
            column[row] = row < offset ? Rgb.Black : image[x, row - offset];
    }
}
