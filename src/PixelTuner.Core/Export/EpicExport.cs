using PixelTuner.Core.Imaging;
using PixelTuner.Core.Media;
using PixelTuner.Core.Model;

namespace PixelTuner.Core.Export;

/// <summary>
/// Builds the E-Pic poi .bin of E-Pic timelines the way PixelTuner 2's
/// FileViewModel.GetEpicBinaryPresentation did. Each item is a gradient picture of which
/// the poi plays the top row, one colour per column.
/// </summary>
/// <remarks>
/// Kept as PixelTuner 2 had it: the timeline's colour tag is written as it is (not divided by
/// 10 as for pixel devices); gaps are one black column with the default stretching; a missing
/// file becomes one black column.
/// </remarks>
public sealed class EpicExport
{
    private readonly IMediaSource _media;
    private readonly Dictionary<string, Rgb[]> _cache = new(StringComparer.Ordinal);

    public EpicExport(IMediaSource media) => _media = media;

    /// <summary>Stretching of the gap fillers: PixelTuner 2's "default stretching" setting.</summary>
    public ushort GapStretching { get; init; } = 4;

    public ColorDepth Depth { get; init; } = ColorDepth.Bit24;

    /// <summary>E-Pic timelines of the show, in show order.</summary>
    public static IReadOnlyList<Timeline> EpicTimelines(ShowProject show) =>
        show.Timelines.Where(t => t.Kind == TimelineKind.Epic).ToList();

    public byte[] Export(IEnumerable<Timeline> timelines)
    {
        var lines = new List<EpicExportTimeline>();
        foreach (var timeline in timelines)
        {
            if (timeline.Kind != TimelineKind.Epic || timeline.Items.Count == 0) continue;
            var line = new EpicExportTimeline { Color = ColorText.Parse(timeline.Color) };
            var items = timeline.Items;
            if (items[0].Start != TimeSpan.Zero) line.Items.Add(Gap(TimeSpan.Zero, items[0].Start));
            for (int i = 0; i < items.Count; i++)
            {
                line.Items.Add(Item(items[i]));
                if (i + 1 < items.Count && items[i].End != items[i + 1].Start)
                    line.Items.Add(Gap(items[i].End, items[i + 1].Start));
            }
            lines.Add(line);
        }
        return EpicBinaryWriter.Write(lines, Depth);
    }

    private EpicExportItem Item(TimelineItem item) => new()
    {
        ImageKey = PixelExport.ImageKey(item),
        Colors = TopRow(item.ResourceId),
        StartMs = PixelExport.Milliseconds(item.Start),
        DurationMs = PixelExport.Milliseconds(item.Duration),
        Stretching = unchecked((ushort)item.Stretching),
        Brightness = (byte)item.Brightness,
        LaserOn = item.LaserOn,
    };

    private EpicExportItem Gap(TimeSpan start, TimeSpan end) => new()
    {
        ImageKey = null,
        Colors = [Rgb.Black],
        StartMs = PixelExport.Milliseconds(start),
        DurationMs = PixelExport.Milliseconds(end - start),
        Stretching = GapStretching,
    };

    private Rgb[] TopRow(string? resourceId)
    {
        if (resourceId is null) return [Rgb.Black];
        if (!_cache.TryGetValue(resourceId, out var row))
        {
            using var stream = _media.Open(resourceId);
            var image = ImageDecoder.Decode(stream);
            row = new Rgb[image.Width];
            for (int x = 0; x < image.Width; x++) row[x] = image[x, 0];
            _cache[resourceId] = row;
        }
        return row;
    }
}
