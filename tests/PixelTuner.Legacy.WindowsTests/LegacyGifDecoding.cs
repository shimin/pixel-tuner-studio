using System.IO;
using System.Windows.Media.Imaging;
using PixelTuner.Core.Imaging;
using Xunit.Abstractions;
using SD = System.Drawing;

namespace PixelTuner.Legacy.WindowsTests;

/// <summary>
/// PixelTuner 2 read video GIFs frame by frame through WPF's GifBitmapDecoder, a BMP
/// round trip and GDI+ (ImagesCollectionVideoItem). <see cref="GifFrames"/> must return
/// the same pixels, or video-device .bin files differ from what PixelTuner 2 wrote.
/// </summary>
public class LegacyGifDecoding(ITestOutputHelper output)
{
    [Fact]
    public void Every_gif_in_the_reference_set_decodes_like_PixelTuner_2()
    {
        string? root = Environment.GetEnvironmentVariable("PIXELTUNER_SAMPLES");
        if (string.IsNullOrEmpty(root))
        {
            output.WriteLine("PIXELTUNER_SAMPLES is not set, nothing to check.");
            return;
        }
        string? report = Environment.GetEnvironmentVariable("PIXELTUNER_REPORT");

        var failures = new List<string>();
        int files = 0;
        foreach (string path in Directory.EnumerateFiles(root, "*.gif", SearchOption.AllDirectories).Order())
        {
            files++;
            string name = Path.GetRelativePath(root, path);
            var ours = GifFrames.DecodeFile(path);
            var theirs = DecodeLikePixelTuner2(path);
            var lines = new List<string>();
            if (ours.Count != theirs.Count) lines.Add($"frame count: ours {ours.Count}, PixelTuner 2 {theirs.Count}");
            for (int f = 0; f < Math.Min(ours.Count, theirs.Count); f++)
            {
                var (image, format, palette) = theirs[f];
                var mine = ours[f];
                if (mine.Width != image.Width || mine.Height != image.Height)
                {
                    lines.Add($"frame {f} ({format}, palette {palette}): size ours {mine.Width}x{mine.Height}, PixelTuner 2 {image.Width}x{image.Height}");
                    continue;
                }
                for (int y = 0; y < image.Height; y++)
                    for (int x = 0; x < image.Width; x++)
                        if (mine[x, y] != image[x, y])
                            lines.Add($"frame {f} ({format}, palette {palette}) at {x},{y}: ours {mine[x, y]}, PixelTuner 2 {image[x, y]}");
            }
            if (lines.Count == 0) continue;
            failures.Add($"{name}: {lines.Count} differences, first: {lines[0]}");
            if (!string.IsNullOrEmpty(report))
            {
                Directory.CreateDirectory(report);
                File.WriteAllLines(Path.Combine(report, name.Replace(Path.DirectorySeparatorChar, '_') + ".txt"), lines);
                DumpFrames(Path.Combine(report, name.Replace(Path.DirectorySeparatorChar, '_') + ".rgb"), theirs);
            }
        }

        foreach (var f in failures) output.WriteLine(f);
        output.WriteLine($"{files} GIF files checked, {failures.Count} differ.");
        Assert.True(failures.Count == 0, $"{failures.Count} of {files} GIF files decode differently.");
    }

    /// <summary>ImagesCollectionVideoItem.Load and ImageHelper.BitmapImageToBitmap, step by step.</summary>
    private static List<(PixelImage Image, string Format, int Palette)> DecodeLikePixelTuner2(string path)
    {
        var result = new List<(PixelImage, string, int)>();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var decoder = new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
        foreach (var frame in decoder.Frames)
        {
            using var bmp = new MemoryStream();
            var encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(frame));
            encoder.Save(bmp);
            using var loaded = new SD.Bitmap(bmp);
            using var copy = new SD.Bitmap(loaded);
            var image = new PixelImage(copy.Width, copy.Height);
            for (int y = 0; y < copy.Height; y++)
                for (int x = 0; x < copy.Width; x++)
                {
                    var c = copy.GetPixel(x, y);
                    image[x, y] = new Rgb(c.R, c.G, c.B);
                }
            result.Add((image, frame.Format.ToString(), frame.Palette?.Colors.Count ?? 0));
        }
        return result;
    }

    /// <summary>Raw dump for offline comparison: per frame u16 width, u16 height, then RGB rows.</summary>
    private static void DumpFrames(string path, List<(PixelImage Image, string Format, int Palette)> frames)
    {
        using var file = new BinaryWriter(File.Create(path));
        foreach (var (image, _, _) in frames)
        {
            file.Write((ushort)image.Width);
            file.Write((ushort)image.Height);
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    var c = image[x, y];
                    file.Write(c.R); file.Write(c.G); file.Write(c.B);
                }
        }
    }
}
