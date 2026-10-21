using PixelTuner.Core.Imaging;
using PixelTuner.Core.Model;
using static PixelTuner.Core.Export.ShowMedia;

namespace PixelTuner.Core.Export;

/// <summary>
/// Builds the LightMaster .bin the way PixelTuner 2's FileViewModel.GetLightBinaryPresentation
/// did: solid colours and effects of the light timelines, with black in the gaps.
/// </summary>
public static class LightExport
{
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
                var item = Item(items[i]);
                if (item is not null) line.Items.Add(item);
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
        uint start = Milliseconds(item.Start), end = Milliseconds(item.End);
        if (item.Kind == ItemKind.Solid)
        {
            var color = ColorText.ParseRgb(item.Color);
            return new LightExportItem { Effect = LightEffect.Solid, StartMs = start, EndMs = end, ColorFirst = color, ColorLast = color };
        }
        if (item.Kind != ItemKind.Effect) return null;

        var effect = item.Effect switch
        {
            LightEffectKind.LinearTransition => LightEffect.LinearTransition,
            LightEffectKind.SineWave => LightEffect.SineWave,
            LightEffectKind.SquareWave => LightEffect.SquareWave,
            LightEffectKind.SawToothWave => LightEffect.SawToothWave,
            _ => throw new InvalidDataException($"Unknown light effect '{item.Effect}'."),
        };
        return new LightExportItem
        {
            Effect = effect,
            StartMs = start,
            EndMs = end,
            ColorFirst = ColorText.ParseRgb(item.Color),
            ColorLast = ColorText.ParseRgb(item.ColorLast),
            T1 = item.T1,
            T2 = item.T2,
        };
    }

    private static LightExportItem Black(TimeSpan start, TimeSpan end) => new()
    {
        Effect = LightEffect.Solid,
        StartMs = Milliseconds(start),
        EndMs = Milliseconds(end),
        ColorFirst = Rgb.Black,
        ColorLast = Rgb.Black,
    };
}
