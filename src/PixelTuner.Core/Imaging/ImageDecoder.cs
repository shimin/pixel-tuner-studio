using SkiaSharp;

namespace PixelTuner.Core.Imaging;

/// <summary>
/// Decodes BMP, PNG, JPEG and GIF files into <see cref="PixelImage"/> with SkiaSharp,
/// which works the same on Windows, macOS, iOS and Android.
/// </summary>
/// <remarks>
/// BMP, PNG and GIF decode to exactly the pixels PixelTuner 2 read with GDI+.
/// JPEG decoders round differently: some pixels differ by one level (out of 255)
/// from what PixelTuner 2 produced. GIF gives its first frame, as GDI+ did.
/// </remarks>
public static class ImageDecoder
{
    public static PixelImage Decode(Stream stream)
    {
        using var data = SKData.Create(stream)
            ?? throw new InvalidDataException("Cannot read the image data.");
        return Decode(data);
    }

    public static PixelImage Decode(ReadOnlySpan<byte> bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        return Decode(data);
    }

    public static PixelImage DecodeFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Decode(stream);
    }

    private static PixelImage Decode(SKData data)
    {
        using var codec = SKCodec.Create(data)
            ?? throw new InvalidDataException("Unknown or damaged image format.");

        // Unpremultiplied RGBA keeps the stored colour of semi-transparent pixels,
        // which is what GDI+ GetPixel returned.
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            throw new InvalidDataException($"Cannot decode the image: {result}.");

        var bytes = bitmap.GetPixelSpan();
        var pixels = new Rgb[info.Width * info.Height];
        int rowBytes = bitmap.RowBytes;
        for (int y = 0; y < info.Height; y++)
        {
            var row = bytes.Slice(y * rowBytes, info.Width * 4);
            for (int x = 0; x < info.Width; x++)
                pixels[y * info.Width + x] = new Rgb(row[x * 4], row[x * 4 + 1], row[x * 4 + 2]);
        }
        return new PixelImage(info.Width, info.Height, pixels);
    }
}
