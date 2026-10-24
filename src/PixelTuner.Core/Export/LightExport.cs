using PixelTuner.Core.Imaging;
using PixelTuner.Core.Model;

namespace PixelTuner.Core.Export;

/// <summary>
/// Builds the LightMaster .bin of light timelines the way PixelTuner 2's
/// FileViewModel.GetLightBinaryPresentation did: solid colours and effects, with black in the gaps.
/// </summary>
public static class LightExport
{
    /// <summary>Light timelines of the show, in show order.</summary>
    public static IReadOnlyList<Timeline> LightTimelines(ShowProject show) =>
        show.Timelines.Where(t => t.Kind == TimelineKind.Light).ToList();

    /// <param name="rate">Sampling step in milliseconds, PixelTuner 2's "discreteness" setting.</param>
    public static byte[] Export(IEnumerable<Timeline> timelines, int rate = 5)
    {
        var lines = new List<LightExportTimeline>();
        foreach (var timeline in timelines)
        {
            if (timeline.Kind != TimelineKind.Light || timeline.Items.Count == 0) continue;
            var line = new LightExportTimeline { IsMonochrome = timeline.Monochrome };
            var items = timeline.Items;
            if (items[0].Start != TimeSpan.Zero) line.Items.Add(Black(TimeSpan.Zero, items[0].Start));
            for (int i = 0; i < items.Count; i++)
            {
                if (Item(items[i]) is { } item) line.Items.Add(item);
                if (i + 1 < items.Count && items[i].End != items[i + 1].Start)
                    line.Items.Add(Black(items[i].End, items[i + 1].Start));
            }
            lines.Add(line);
        }
        return LightBinaryWriter.Write(lines, rate);
    }

    /// <summary>PixelTuner 2 skipped items that were neither a solid colour nor an effect.</summary>
    private static LightExportItem? Item(TimelineItem item)
    {
        uint start = PixelExport.Milliseconds(item.Start), end = PixelExport.Milliseconds(item.End);
        if (item.Kind == ItemKind.Solid)
        {
            var color = ColorText.Parse(item.Color);
            return new LightExportItem { Effect = LightEffect.Solid, StartMs = start, EndMs = end, ColorFirst = color, ColorLast = color };
        }
        if (item.Kind != ItemKind.Effect) return null;

        var effect = item.Effect switch
        {
            EffectKind.LinearTransition => LightEffect.LinearTransition,
            EffectKind.SineWave => LightEffect.SineWave,
            EffectKind.SquareWave => LightEffect.SquareWave,
            EffectKind.SawToothWave => LightEffect.SawToothWave,
            _ => throw new InvalidDataException($"Unknown light effect '{item.Effect}'."),
        };
        return new LightExportItem
        {
            Effect = effect,
            StartMs = start,
            EndMs = end,
            ColorFirst = ColorText.Parse(item.Color),
            ColorLast = ColorText.Parse(item.ColorLast),
            T1 = item.T1 ?? 0,
            T2 = item.T2 ?? 0,
        };
    }

    private static LightExportItem Black(TimeSpan start, TimeSpan end) => new()
    {
        Effect = LightEffect.Solid,
        StartMs = PixelExport.Milliseconds(start),
        EndMs = PixelExport.Milliseconds(end),
        ColorFirst = Rgb.Black,
        ColorLast = Rgb.Black,
    };
}
