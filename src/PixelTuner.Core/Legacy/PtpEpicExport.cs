using PixelTuner.Core.Export;
using PixelTuner.Core.Imaging;

namespace PixelTuner.Core.Legacy;

/// <summary>
/// Builds the E-Pic poi .bin from a PixelTuner 2 project the way PixelTuner 2's
/// FileViewModel.GetEpicBinaryPresentation did. Each item is a gradient picture of which
/// the poi plays the top row, one colour per column.
/// </summary>
/// <remarks>
/// Kept as PixelTuner 2 had it: the timeline's colour tag is written as it is (not divided by
/// 10 as for pixel devices); gaps are one black column with the default stretching; a missing
/// file becomes one black column.
/// </remarks>
public sealed class PtpEpicExport
{
    private readonly Func<string, string?> _resolveMedia;
    private readonly Dictionary<string, Rgb[]> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="resolveMedia">Readable path of an item's file, or null when the file is missing.</param>
    public PtpEpicExport(Func<string, string?> resolveMedia) => _resolveMedia = resolveMedia;

    /// <summary>Stretching of the gap fillers: PixelTuner 2's "default stretching" setting.</summary>
    public ushort GapStretching { get; init; } = 4;

    public ColorDepth Depth { get; init; } = ColorDepth.Bit24;

    /// <summary>E-Pic timelines of the project, in project order.</summary>
    public static IReadOnlyList<PtpTimeline> EpicTimelines(PtpDocument document) =>
        document.Timelines.Where(t => t.Type == PtpTimeline.EpicType).ToList();

    public byte[] Export(IEnumerable<PtpTimeline> timelines)
    {
        var lines = new List<EpicExportTimeline>();
        foreach (var timeline in timelines)
        {
            if (timeline.Type != PtpTimeline.EpicType || timeline.Items.Count == 0) continue;
            var (_, r, g, b) = timeline.Color;
            var line = new EpicExportTimeline { Color = new Rgb(r, g, b) };
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

    private EpicExportItem Item(PtpItem item) => new()
    {
        ImageKey = item.FullName.ToLowerInvariant(),
        Colors = TopRow(item.FullName),
        StartMs = Milliseconds(item.Start),
        DurationMs = Milliseconds(item.End - item.Start),
        Stretching = unchecked((ushort)item.Stretching),
        Brightness = item.Brightness,
        LaserOn = item.LaserOn,
    };

    private EpicExportItem Gap(TimeSpan start, TimeSpan end) => new()
    {
        ImageKey = null,
        Colors = [Rgb.Black],
        StartMs = Milliseconds(start),
        DurationMs = Milliseconds(end - start),
        Stretching = GapStretching,
    };

    private Rgb[] TopRow(string fullName)
    {
        if (!_cache.TryGetValue(fullName, out var row))
        {
            string? path = _resolveMedia(fullName);
            if (path is null) row = [Rgb.Black];
            else
            {
                var image = ImageDecoder.DecodeFile(path);
                row = new Rgb[image.Width];
                for (int x = 0; x < image.Width; x++) row[x] = image[x, 0];
            }
            _cache[fullName] = row;
        }
        return row;
    }

    private static uint Milliseconds(TimeSpan t) => checked((uint)Math.Round(t.TotalMilliseconds));
}
