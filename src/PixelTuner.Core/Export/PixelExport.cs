using PixelTuner.Core.Imaging;
using PixelTuner.Core.Media;
using PixelTuner.Core.Model;

namespace PixelTuner.Core.Export;

/// <summary>
/// Builds the pixel-device .bin (poi, staves, hoops, fans) of pixel timelines the way
/// PixelTuner 2 did: gaps filled with black, timeline height taken from its first image.
/// </summary>
public sealed class PixelExport
{
    /// <summary>Width of the black filler PixelTuner 2 put into gaps between items.</summary>
    private const int GapWidth = 10;

    private readonly IMediaSource _media;
    private readonly Dictionary<string, PixelImage> _cache = new(StringComparer.Ordinal);

    public PixelExport(IMediaSource media) => _media = media;

    /// <summary>
    /// Stretching of the gap fillers. PixelTuner 2 took it from its "default stretching"
    /// setting (4 unless the user changed it).
    /// </summary>
    public ushort GapStretching { get; init; } = 4;

    public ColorDepth Depth { get; init; } = ColorDepth.Bit24;

    /// <summary>Pixel timelines of the show, in show order.</summary>
    public static IReadOnlyList<Timeline> PixelTimelines(ShowProject show) =>
        show.Timelines.Where(t => t.Kind == TimelineKind.Pixel).ToList();

    /// <summary>Height in LEDs of a timeline: the height of its first image.</summary>
    public int TimelineHeight(Timeline timeline) =>
        timeline.Items.Count == 0 ? 0 : Load(timeline.Items[0]).Height;

    /// <summary>
    /// The .bin for the given pixel timelines. Timelines whose height differs from
    /// <paramref name="height"/> are left out, as PixelTuner 2 offered only matching ones.
    /// </summary>
    public byte[] Export(IEnumerable<Timeline> timelines, ushort height)
    {
        var lines = new List<PixelExportTimeline>();
        foreach (var timeline in timelines)
        {
            if (timeline.Kind != TimelineKind.Pixel || timeline.Items.Count == 0) continue;
            int lineHeight = TimelineHeight(timeline);
            if (lineHeight != height) continue;
            var c = ColorText.Parse(timeline.Color);
            lines.Add(BuildTimeline(timeline, lineHeight, new Rgb((byte)(c.R / 10), (byte)(c.G / 10), (byte)(c.B / 10))));
        }
        return PixelBinaryWriter.Write(lines, height, Depth);
    }

    /// <summary>
    /// The .bin PixelTuner 2 uploaded to a fan (FileViewModel.GetFanBinaryPresentation):
    /// one pixel timeline at the height of its first image. Unlike the pixel export, the
    /// timeline keeps the default menu colour (0, 0, 20) instead of its own colour tag.
    /// </summary>
    public byte[] ExportFan(Timeline timeline)
    {
        if (timeline.Kind != TimelineKind.Pixel)
            throw new ArgumentException("A fan plays a pixel timeline.", nameof(timeline));
        if (timeline.Items.Count == 0)
            throw new ArgumentException("The timeline has no items.", nameof(timeline));
        int height = TimelineHeight(timeline);
        var line = BuildTimeline(timeline, height, new PixelExportTimeline().Color);
        return PixelBinaryWriter.Write([line], checked((ushort)height), Depth);
    }

    private PixelExportTimeline BuildTimeline(Timeline timeline, int lineHeight, Rgb color)
    {
        var result = new PixelExportTimeline { Color = color };
        var items = timeline.Items;
        if (items[0].Start != TimeSpan.Zero)
            result.Items.Add(Gap(TimeSpan.Zero, items[0].Start, lineHeight, timeline.FlipVertical));
        for (int i = 0; i < items.Count; i++)
        {
            result.Items.Add(Item(items[i], timeline.FlipVertical));
            if (i + 1 < items.Count && items[i].End != items[i + 1].Start)
                result.Items.Add(Gap(items[i].End, items[i + 1].Start, lineHeight, timeline.FlipVertical));
        }
        return result;
    }

    private PixelExportItem Item(TimelineItem item, bool flip) => new()
    {
        ImageKey = ImageKey(item),
        Image = Load(item),
        StartMs = Milliseconds(item.Start),
        DurationMs = Milliseconds(item.Duration),
        Stretching = unchecked((ushort)item.Stretching),
        Brightness = (byte)item.Brightness,
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

    private PixelImage Load(TimelineItem item)
    {
        if (item.ResourceId is not { } id) throw new MissingMediaException(item.SourceName);
        if (!_cache.TryGetValue(id, out var image))
        {
            using var stream = _media.Open(id);
            _cache[id] = image = ImageDecoder.Decode(stream);
        }
        return image;
    }

    /// <summary>
    /// Items share an image when they have the same content and the same file name. PixelTuner 2
    /// matched by lower-cased path, so two files with equal content stay two images, as there.
    /// A missing file is told apart by its name.
    /// </summary>
    internal static string ImageKey(TimelineItem item) =>
        $"{item.ResourceId ?? "missing"}|{item.SourceName?.ToLowerInvariant()}";

    internal static uint Milliseconds(TimeSpan t) => checked((uint)Math.Round(t.TotalMilliseconds));
}
