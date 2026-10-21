using PixelTuner.Core.Export;
using PixelTuner.Core.Imaging;

namespace PixelTuner.Core.Legacy;

/// <summary>
/// Builds the pixel-device .bin from a PixelTuner 2 project the way PixelTuner 2's
/// "Export bin" did: gaps filled with black, timeline height taken from its first image.
/// </summary>
public sealed class PtpPixelExport
{
    /// <summary>Width of the black filler PixelTuner 2 put into gaps between items.</summary>
    private const int GapWidth = 10;

    private readonly Func<string, PixelImage> _loadImage;
    private readonly Dictionary<string, PixelImage> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="resolveMedia">Turns an item's stored path into a readable file path.</param>
    public PtpPixelExport(Func<string, string> resolveMedia)
        : this(name => ImageDecoder.DecodeFile(resolveMedia(name)))
    {
    }

    public PtpPixelExport(Func<string, PixelImage> loadImage) => _loadImage = loadImage;

    /// <summary>
    /// Stretching of the gap fillers. PixelTuner 2 took it from its "default stretching"
    /// setting (4 unless the user changed it).
    /// </summary>
    public ushort GapStretching { get; init; } = 4;

    public ColorDepth Depth { get; init; } = ColorDepth.Bit24;

    /// <summary>Pixel timelines of the project, in project order.</summary>
    public static IReadOnlyList<PtpTimeline> PixelTimelines(PtpDocument document) =>
        document.Timelines.Where(t => t.Type == PtpTimeline.PixelType).ToList();

    /// <summary>Height in LEDs of a timeline: the height of its first image.</summary>
    public int TimelineHeight(PtpTimeline timeline) =>
        timeline.Items.Count == 0 ? 0 : Load(timeline.Items[0].FullName).Height;

    /// <summary>
    /// The .bin for the given pixel timelines. Timelines whose height differs from
    /// <paramref name="height"/> are left out, as PixelTuner 2 offered only matching ones.
    /// </summary>
    public byte[] Export(IEnumerable<PtpTimeline> timelines, ushort height)
    {
        var lines = new List<PixelExportTimeline>();
        foreach (var timeline in timelines)
        {
            if (timeline.Type != PtpTimeline.PixelType || timeline.Items.Count == 0) continue;
            int lineHeight = TimelineHeight(timeline);
            if (lineHeight != height) continue;
            lines.Add(BuildTimeline(timeline, lineHeight));
        }
        return PixelBinaryWriter.Write(lines, height, Depth);
    }

    private PixelExportTimeline BuildTimeline(PtpTimeline timeline, int lineHeight)
    {
        var (_, r, g, b) = timeline.Color;
        var result = new PixelExportTimeline { Color = new Rgb((byte)(r / 10), (byte)(g / 10), (byte)(b / 10)) };

        var items = timeline.Items;
        if (items[0].Start != TimeSpan.Zero)
            result.Items.Add(Gap(TimeSpan.Zero, items[0].Start, lineHeight, timeline.IsFlippedVertical));
        for (int i = 0; i < items.Count; i++)
        {
            result.Items.Add(Item(items[i], timeline.IsFlippedVertical));
            if (i + 1 < items.Count && items[i].End != items[i + 1].Start)
                result.Items.Add(Gap(items[i].End, items[i + 1].Start, lineHeight, timeline.IsFlippedVertical));
        }
        return result;
    }

    private PixelExportItem Item(PtpItem item, bool flip) => new()
    {
        // PixelTuner 2 matched images by lower-cased path.
        ImageKey = item.FullName.ToLowerInvariant(),
        Image = Load(item.FullName),
        StartMs = Milliseconds(item.Start),
        DurationMs = Milliseconds(item.End - item.Start),
        Stretching = (ushort)item.Stretching,
        Brightness = item.Brightness,
        LaserOn = item.LaserOn,
        FlipVertical = flip,
    };

    private PixelExportItem Gap(TimeSpan start, TimeSpan end, int height, bool flip) => new()
    {
        ImageKey = null,
        Image = PixelImage.Black(GapWidth, height),
        StartMs = Milliseconds(start),
        DurationMs = Milliseconds(end - start),
        Stretching = GapStretching,
        FlipVertical = flip,
    };

    private PixelImage Load(string fullName)
    {
        if (!_cache.TryGetValue(fullName, out var image))
            _cache[fullName] = image = _loadImage(fullName);
        return image;
    }

    private static uint Milliseconds(TimeSpan t) => checked((uint)Math.Round(t.TotalMilliseconds));
}
