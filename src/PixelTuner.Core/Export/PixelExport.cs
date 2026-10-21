using PixelTuner.Core.Imaging;
using PixelTuner.Core.Model;
using static PixelTuner.Core.Export.ShowMedia;

namespace PixelTuner.Core.Export;

/// <summary>
/// Builds the pixel-device .bin (poi, staves, hoops, fans) from pixel timelines the way
/// PixelTuner 2's "Export bin" did: gaps filled with black, timeline height taken from
/// its first image.
/// </summary>
public sealed class PixelExport(ShowMedia media)
{
    /// <summary>Width of the black filler PixelTuner 2 put into gaps between items.</summary>
    private const int GapWidth = 10;

    /// <summary>
    /// Stretching of the gap fillers. PixelTuner 2 took it from its "default stretching"
    /// setting (4 unless the user changed it).
    /// </summary>
    public ushort GapStretching { get; init; } = 4;

    public ColorDepth Depth { get; init; } = ColorDepth.Bit24;

    /// <summary>Height in LEDs of a timeline: the height of its first image.</summary>
    /// <exception cref="FileNotFoundException">The first item's file is missing.</exception>
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
            lines.Add(BuildTimeline(timeline, lineHeight));
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

    private PixelExportTimeline BuildTimeline(Timeline timeline, int lineHeight, Rgb? color = null)
    {
        var (_, r, g, b) = ColorText.ParseArgb(timeline.Color);
        var result = new PixelExportTimeline { Color = color ?? new Rgb((byte)(r / 10), (byte)(g / 10), (byte)(b / 10)) };

        var items = timeline.Items;
        bool flip = timeline.FlipVertical;
        if (items[0].Start != TimeSpan.Zero)
            result.Items.Add(Gap(TimeSpan.Zero, items[0].Start, lineHeight, flip));
        for (int i = 0; i < items.Count; i++)
        {
            result.Items.Add(Item(items[i], flip));
            if (i + 1 < items.Count && items[i].End != items[i + 1].Start)
                result.Items.Add(Gap(items[i].End, items[i + 1].Start, lineHeight, flip));
        }
        return result;
    }

    private PixelExportItem Item(TimelineItem item, bool flip) => new()
    {
        ImageKey = MediaKey(item),
        Image = Load(item),
        StartMs = Milliseconds(item.Start),
        DurationMs = Milliseconds(item.Duration),
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

    private PixelImage Load(TimelineItem item) => item.ResourceId is { } id
        ? media.Image(id)
        : throw new FileNotFoundException($"Media file '{item.SourcePath}' of the project was not found.", item.SourcePath);
}
