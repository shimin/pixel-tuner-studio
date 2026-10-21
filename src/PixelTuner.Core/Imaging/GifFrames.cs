using System.Buffers.Binary;

namespace PixelTuner.Core.Imaging;

/// <summary>
/// Reads the frames of a GIF one by one, each as it is stored in the file: the frame's
/// own rectangle, colours straight from its palette, no compositing with earlier frames
/// and no transparency. This is what PixelTuner 2 got from WPF's GifBitmapDecoder and
/// sampled for video devices, so the .bin export needs exactly this and not the
/// composited animation that ordinary decoders (SkiaSharp too) return.
/// </summary>
public static class GifFrames
{
    public static IReadOnlyList<PixelImage> DecodeFile(string path) => Decode(File.ReadAllBytes(path));

    public static IReadOnlyList<PixelImage> Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < 13 || data[0] != 'G' || data[1] != 'I' || data[2] != 'F')
            throw new InvalidDataException("Not a GIF file.");

        int pos = 6;
        int screenFlags = data[pos + 4];
        pos += 7;
        Rgb[]? globalPalette = null;
        if ((screenFlags & 0x80) != 0)
            globalPalette = ReadPalette(data, ref pos, screenFlags & 7);

        var frames = new List<PixelImage>();
        while (pos < data.Length)
        {
            byte block = data[pos++];
            if (block == 0x3B) break;                      // trailer
            if (block == 0x21)                             // extension: skip, nothing in it matters here
            {
                pos++;
                SkipSubBlocks(data, ref pos);
                continue;
            }
            if (block != 0x2C) throw new InvalidDataException($"Unexpected GIF block 0x{block:X2}.");

            int width = BinaryPrimitives.ReadUInt16LittleEndian(data[(pos + 4)..]);
            int height = BinaryPrimitives.ReadUInt16LittleEndian(data[(pos + 6)..]);
            int flags = data[pos + 8];
            pos += 9;
            var palette = (flags & 0x80) != 0 ? ReadPalette(data, ref pos, flags & 7) : globalPalette;
            int minCodeSize = data[pos++];
            var indices = Lzw(data, ref pos, minCodeSize, width * height);
            frames.Add(ToImage(indices, width, height, palette, interlaced: (flags & 0x40) != 0));
        }
        return frames;
    }

    private static Rgb[] ReadPalette(ReadOnlySpan<byte> data, ref int pos, int sizeBits)
    {
        int count = 2 << sizeBits;
        var palette = new Rgb[count];
        for (int i = 0; i < count; i++, pos += 3)
            palette[i] = new Rgb(data[pos], data[pos + 1], data[pos + 2]);
        return palette;
    }

    private static void SkipSubBlocks(ReadOnlySpan<byte> data, ref int pos)
    {
        while (pos < data.Length && data[pos] != 0) pos += data[pos] + 1;
        pos++;
    }

    private static PixelImage ToImage(byte[] indices, int width, int height, Rgb[]? palette, bool interlaced)
    {
        var image = new PixelImage(width, height);
        int[] rows = interlaced ? InterlacedRows(height) : [.. Enumerable.Range(0, height)];
        for (int i = 0; i < height; i++)
        {
            int y = rows[i];
            for (int x = 0; x < width; x++)
            {
                int index = indices[i * width + x];
                image[x, y] = palette is not null && index < palette.Length ? palette[index] : Rgb.Black;
            }
        }
        return image;
    }

    /// <summary>Row order of an interlaced frame: every 8th from 0, every 8th from 4, every 4th from 2, every 2nd from 1.</summary>
    private static int[] InterlacedRows(int height)
    {
        var rows = new List<int>(height);
        foreach (var (start, step) in new[] { (0, 8), (4, 8), (2, 4), (1, 2) })
            for (int y = start; y < height; y += step) rows.Add(y);
        return [.. rows];
    }

    /// <summary>GIF LZW: variable code size up to 12 bits, clear and end codes.</summary>
    private static byte[] Lzw(ReadOnlySpan<byte> data, ref int pos, int minCodeSize, int pixelCount)
    {
        var output = new byte[pixelCount];
        int outPos = 0;

        int clear = 1 << minCodeSize;
        int end = clear + 1;
        var prefix = new short[4096];
        var suffix = new byte[4096];
        var stack = new byte[4097];

        int codeSize = minCodeSize + 1;
        int next = clear + 2;
        int previous = -1;
        byte first = 0;

        int bits = 0, buffer = 0;
        bool done = false;
        while (pos < data.Length)
        {
            int blockSize = data[pos++];
            if (blockSize == 0) break;
            int blockEnd = pos + blockSize;
            while (pos < blockEnd)
            {
                buffer |= data[pos++] << bits;
                bits += 8;
                while (!done && bits >= codeSize)
                {
                    int code = buffer & ((1 << codeSize) - 1);
                    buffer >>= codeSize;
                    bits -= codeSize;

                    if (code == clear)
                    {
                        codeSize = minCodeSize + 1;
                        next = clear + 2;
                        previous = -1;
                        continue;
                    }
                    if (code == end) { done = true; break; }

                    int top = 0;
                    int current = code;
                    if (previous == -1)
                    {
                        first = (byte)code;
                        if (outPos < output.Length) output[outPos++] = first;
                        previous = code;
                        continue;
                    }
                    if (code >= next)
                    {
                        // Code not in the table yet: previous string + its first character.
                        stack[top++] = first;
                        current = previous;
                    }
                    while (current >= clear)
                    {
                        stack[top++] = suffix[current];
                        current = prefix[current];
                    }
                    first = (byte)current;
                    stack[top++] = first;
                    while (top > 0)
                    {
                        byte value = stack[--top];
                        if (outPos < output.Length) output[outPos++] = value;
                    }

                    if (next < 4096)
                    {
                        prefix[next] = (short)previous;
                        suffix[next] = first;
                        next++;
                        if (next == 1 << codeSize && codeSize < 12) codeSize++;
                    }
                    previous = code;
                }
            }
            pos = blockEnd;
        }
        return output;
    }
}
