using Avalonia.Media.Imaging;
using PixelTuner.Core.Imaging;
using PixelTuner.Core.Model;
using PixelTuner.UI.ViewModels;

namespace PixelTuner.UI.Timelines;

/// <summary>What a timeline item looks like when it is drawn on its lane.</summary>
/// <param name="Image">One pass of the item as the device shows it: a column per image column (frame, colour) and a row per LED band.</param>
/// <param name="Columns">Columns of the device image; with the stretching it sets how long one pass takes.</param>
/// <param name="Average">The colour of the item seen from far away.</param>
public sealed record Thumbnail(PixelImage Image, int Columns, Rgb Average)
{
    private Bitmap? _bitmap;

    /// <summary>The image for drawing; made on first use, because only the window can make it.</summary>
    public Bitmap? Bitmap => _bitmap ??= PixelBitmap.From(Image);

    /// <summary>How long one pass takes at this stretching; the device repeats the item from its start to its end.</summary>
    public TimeSpan Pass(TimelineItem item) => item.Kind switch
    {
        ItemKind.Video => ItemPreview.VideoPassDuration(Columns, item.Stretching),
        ItemKind.Gradient => ItemPreview.EpicPassDuration(Columns, item.Stretching),
        _ => ItemPreview.PassDuration(Columns, item.Stretching),
    };
}

/// <summary>
/// Thumbnails of the items of a project, made once per picture, brightness and flip. A picture is decoded on
/// first use; a missing or damaged file gives none, and the lane then draws the item as a plain block.
/// </summary>
public sealed class ItemThumbnails(Func<string, Stream?> open)
{
    /// <summary>A video frame becomes this many rows of average colour; a lane is not taller.</summary>
    private const int VideoRows = 16;

    private readonly Dictionary<(string Id, string Kind, byte Brightness, bool Flip), Thumbnail?> _cache = [];

    public Thumbnail? Get(TimelineItem item, Timeline timeline)
    {
        if (item.ResourceId is not { } id || item.Kind is not (ItemKind.Image or ItemKind.Video or ItemKind.Gradient)) return null;
        var key = (id, item.Kind, item.Brightness, item.Kind == ItemKind.Image && timeline.FlipVertical);
        if (_cache.TryGetValue(key, out var found)) return found;
        return _cache[key] = Make(id, item.Kind, key.Brightness, key.Item4);
    }

    private Thumbnail? Make(string id, string kind, byte brightness, bool flip)
    {
        try
        {
            using var stream = open(id);
            if (stream is null) return null;
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            var bytes = copy.ToArray();

            PixelImage source = kind switch
            {
                ItemKind.Video => FrameProfiles(GifFrames.Decode(bytes)),
                ItemKind.Gradient => TopRow(ImageDecoder.Decode(bytes)),
                _ => ImageDecoder.Decode(bytes),
            };
            var shown = ItemPreview.Render(source, brightness, 1, flip);
            return shown.Width == 0 || shown.Height == 0 ? null : new Thumbnail(shown, shown.Width, Average(shown));
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or KeyNotFoundException or ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>The gradient the E-Pic plays: the top row, one colour per column.</summary>
    private static PixelImage TopRow(PixelImage image)
    {
        var row = new PixelImage(image.Width, 1);
        for (int x = 0; x < image.Width; x++) row[x, 0] = image[x, 0];
        return row;
    }

    /// <summary>A column per frame; each column is the frame squeezed to a few rows of average colour.</summary>
    internal static PixelImage FrameProfiles(IReadOnlyList<PixelImage> frames)
    {
        int rows = Math.Min(VideoRows, frames[0].Height);
        var result = new PixelImage(frames.Count, rows);
        for (int f = 0; f < frames.Count; f++)
        {
            var frame = frames[f];
            for (int r = 0; r < rows; r++)
            {
                int y0 = r * frame.Height / rows, y1 = Math.Max(y0 + 1, (r + 1) * frame.Height / rows);
                long sr = 0, sg = 0, sb = 0, n = 0;
                for (int y = y0; y < y1; y++)
                    for (int x = 0; x < frame.Width; x++)
                    {
                        var c = frame[x, y];
                        sr += c.R; sg += c.G; sb += c.B; n++;
                    }
                result[f, r] = n == 0 ? Rgb.Black : new Rgb((byte)(sr / n), (byte)(sg / n), (byte)(sb / n));
            }
        }
        return result;
    }

    internal static Rgb Average(PixelImage image)
    {
        long r = 0, g = 0, b = 0, n = (long)image.Width * image.Height;
        if (n == 0) return Rgb.Black;
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
            {
                var c = image[x, y];
                r += c.R; g += c.G; b += c.B;
            }
        return new Rgb((byte)(r / n), (byte)(g / n), (byte)(b / n));
    }
}
