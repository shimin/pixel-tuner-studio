using PixelTuner.Core.Export;
using PixelTuner.Core.Imaging;

namespace PixelTuner.Core.Legacy;

/// <summary>
/// Builds the video-device .bin (LED matrices, shields, suits) from a PixelTuner 2 project
/// the way PixelTuner 2's FileViewModel.GetVideoBinaryPresentation did. The file is the same
/// "PPOI" layout as for pixel devices; the "image" of an item is its GIF turned into one
/// column per frame, with one row per LED in wiring order.
/// </summary>
/// <remarks>
/// Kept as PixelTuner 2 had it, because devices were tuned on its output:
/// <list type="bullet">
/// <item>every LED colour is scaled by brightness and the WS2812 table first, then encoded
/// like a pixel image of that height, which applies brightness (and above 150 LEDs the
/// APA102 gamma) once more;</item>
/// <item>the timeline's menu colour is always (0, 0, 20); the laser bit is 1, not 0xffff;</item>
/// <item>items with the same file and brightness share one image even when their offsets differ;</item>
/// <item>with two or more timelines selected only the first two are exported, as one timeline
/// of both LED chains one after the other. Both chains take the second channel's brightness
/// for the first scaling and the first channel's for the second, and pairs are matched by the
/// first channel's file alone.</item>
/// </list>
/// </remarks>
public sealed class PtpVideoExport
{
    /// <summary>A missing file became a 5-column black bitmap in PixelTuner 2.</summary>
    private const int MissingFileWidth = 5;

    private const ushort LaserMask = 1;

    private readonly PtpDocument _document;
    private readonly Func<string, string?> _resolveMedia;
    private readonly Dictionary<string, IReadOnlyList<PixelImage>?> _frames = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="resolveMedia">Readable path of an item's file, or null when the file is missing.</param>
    public PtpVideoExport(PtpDocument document, Func<string, string?> resolveMedia)
    {
        _document = document;
        _resolveMedia = resolveMedia;
    }

    /// <summary>Stretching of the gap fillers: PixelTuner 2's "default stretching" setting.</summary>
    public ushort GapStretching { get; init; } = 4;

    public ColorDepth Depth { get; init; } = ColorDepth.Bit24;

    /// <summary>Video timelines of the project, in project order.</summary>
    public static IReadOnlyList<PtpTimeline> VideoTimelines(PtpDocument document) =>
        document.Timelines.Where(t => t.Type == PtpTimeline.VideoType).ToList();

    /// <summary>LED positions of a timeline's canvas element in wiring order; empty without markup.</summary>
    public IReadOnlyList<(int X, int Y)> Leds(PtpTimeline timeline) =>
        timeline.Attributes.TryGetValue("MetaDataPath", out var path)
        && _document.CanvasElements.TryGetValue(path, out var element)
            ? element.OrderedLeds()
            : [];

    /// <summary>
    /// The .bin for the selected timelines: one timeline alone, or the first two as a pair.
    /// Empty when PixelTuner 2 wrote an empty file (no LED markup or no items).
    /// </summary>
    public byte[] Export(IReadOnlyList<PtpTimeline> timelines)
    {
        if (timelines.Count == 0) return [];
        var result = timelines.Count > 1 ? Pair(timelines[0], timelines[1]) : Single(timelines[0]);
        return result is null ? [] : PixelBinaryWriter.Write([result.Value.Line], result.Value.Height, Depth);
    }

    private (PixelExportTimeline Line, ushort Height)? Single(PtpTimeline timeline)
    {
        var leds = Leds(timeline);
        if (leds.Count == 0 || timeline.Items.Count == 0) return null;

        var line = new PixelExportTimeline();
        foreach (var segment in FillGaps(timeline.Items))
        {
            var item = segment.Item;
            PixelImage image;
            if (item is null) image = PixelImage.Black(1, leds.Count);
            else
            {
                var frames = Frames(item);
                image = frames is null
                    ? PixelImage.Black(MissingFileWidth, leds.Count)
                    : Sample(frames.Count, [(frames, leds, Offset(item))], item.Brightness);
            }
            line.Items.Add(Entry(segment, image));
        }
        return (line, checked((ushort)leds.Count));
    }

    private (PixelExportTimeline Line, ushort Height)? Pair(PtpTimeline first, PtpTimeline second)
    {
        var leds1 = Leds(first);
        var leds2 = Leds(second);
        if (leds1.Count == 0 || first.Items.Count == 0 || leds2.Count == 0 || second.Items.Count == 0)
            return null;

        var segments1 = FillGaps(first.Items);
        var segments2 = FillGaps(second.Items);
        // Every end time on either timeline becomes a boundary on both.
        var bounds = segments1.Concat(segments2).Select(s => s.End).Distinct().Order().ToList();
        if (segments1[^1].End != segments2[^1].End)
            throw new NotSupportedException("PixelTuner 2 could not export two video timelines that end at different times.");
        segments1 = Split(segments1, bounds);
        segments2 = Split(segments2, bounds);

        int height = leds1.Count + leds2.Count;
        var line = new PixelExportTimeline();
        for (int i = 0; i < bounds.Count; i++)
        {
            var a = segments1[i];
            var b = segments2[i];
            var frames1 = a.Item is null ? Blank : Frames(a.Item);
            var frames2 = b.Item is null ? Blank : Frames(b.Item);
            var image = frames1 is null || frames2 is null
                ? PixelImage.Black(MissingFileWidth, height)
                : Sample(Math.Max(frames1.Count, frames2.Count),
                    [(frames1, leds1, Offset(a.Item)), (frames2, leds2, Offset(b.Item))],
                    b.Item?.Brightness ?? 100);
            line.Items.Add(Entry(a, image));
        }
        return (line, checked((ushort)height));
    }

    /// <summary>The one black frame of PixelTuner 2's gap filler GIF.</summary>
    private static readonly IReadOnlyList<PixelImage> Blank = [PixelImage.Black(1, 1)];

    /// <summary>
    /// One column per frame, one row per LED of each chain in turn. Frames of a shorter
    /// animation repeat. Each colour is scaled by <paramref name="brightness"/>, then
    /// through the WS2812 table.
    /// </summary>
    private static PixelImage Sample(int frameCount,
        (IReadOnlyList<PixelImage> Frames, IReadOnlyList<(int X, int Y)> Leds, (int X, int Y) Offset)[] chains,
        byte brightness)
    {
        int height = chains.Sum(c => c.Leds.Count);
        var image = new PixelImage(frameCount, height);
        float multiplier = (float)brightness / 100;
        for (int column = 0; column < frameCount; column++)
        {
            int row = 0;
            foreach (var (frames, leds, offset) in chains)
            {
                var frame = frames[column % frames.Count];
                foreach (var (ledX, ledY) in leds)
                {
                    int x = ledX + offset.X, y = ledY + offset.Y;
                    var c = x >= 0 && y >= 0 && x < frame.Width && y < frame.Height ? frame[x, y] : Rgb.Black;
                    c = new Rgb((byte)(c.R * multiplier), (byte)(c.G * multiplier), (byte)(c.B * multiplier));
                    image[column, row++] = Ws2812Profile.Modify(c);
                }
            }
        }
        return image;
    }

    private PixelExportItem Entry(Segment segment, PixelImage image) => new()
    {
        // PixelTuner 2 matched images by lower-cased path; its gap fillers were unique temp files.
        ImageKey = segment.Item?.FullName.ToLowerInvariant(),
        Image = image,
        StartMs = Milliseconds(segment.Start),
        DurationMs = Milliseconds(segment.End - segment.Start),
        Stretching = segment.Item is null ? GapStretching : (ushort)segment.Item.Stretching,
        Brightness = segment.Item?.Brightness ?? 100,
        LaserOn = segment.Item?.LaserOn ?? false,
        LaserMask = LaserMask,
    };

    private IReadOnlyList<PixelImage>? Frames(PtpItem item)
    {
        if (!_frames.TryGetValue(item.FullName, out var frames))
        {
            string? path = _resolveMedia(item.FullName);
            _frames[item.FullName] = frames = path is null ? null : GifFrames.DecodeFile(path);
        }
        return frames;
    }

    private static (int X, int Y) Offset(PtpItem? item) =>
        item is null ? (0, 0) : (IntAttribute(item, "XOffset"), IntAttribute(item, "YOffset"));

    private static int IntAttribute(PtpItem item, string name) =>
        item.Attributes.TryGetValue(name, out var value)
        && int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int v)
            ? v
            : 0;

    private readonly record struct Segment(PtpItem? Item, TimeSpan Start, TimeSpan End);

    /// <summary>Items with black gap fillers before the first one and between them.</summary>
    private static List<Segment> FillGaps(IReadOnlyList<PtpItem> items)
    {
        var result = new List<Segment>();
        if (items[0].Start != TimeSpan.Zero) result.Add(new Segment(null, TimeSpan.Zero, items[0].Start));
        for (int i = 0; i < items.Count; i++)
        {
            result.Add(new Segment(items[i], items[i].Start, items[i].End));
            if (i + 1 < items.Count && items[i].End != items[i + 1].Start)
                result.Add(new Segment(null, items[i].End, items[i + 1].Start));
        }
        return result;
    }

    /// <summary>Cuts the segments at every boundary, as PixelTuner 2's SplitItems.</summary>
    private static List<Segment> Split(List<Segment> segments, List<TimeSpan> bounds)
    {
        var pieces = new List<Segment>();
        foreach (var segment in segments)
        {
            foreach (var bound in bounds)
            {
                if (bound > segment.Start && bound <= segment.End) pieces.Add(segment);
                if (bound > segment.End) break;
            }
        }
        for (int i = 0; i + 1 < bounds.Count; i++)
        {
            pieces[i] = pieces[i] with { End = bounds[i] };
            pieces[i + 1] = pieces[i + 1] with { Start = bounds[i] };
        }
        return pieces;
    }

    private static uint Milliseconds(TimeSpan t) => checked((uint)Math.Round(t.TotalMilliseconds));
}
