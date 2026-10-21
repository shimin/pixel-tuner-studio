using System.Globalization;
using PixelTuner.Core.Export;
using PixelTuner.Core.Imaging;

namespace PixelTuner.Core.Legacy;

/// <summary>
/// Builds the LightMaster .bin from a PixelTuner 2 project the way PixelTuner 2's
/// FileViewModel.GetLightBinaryPresentation did: solid colours and effects of the light
/// timelines, with black in the gaps.
/// </summary>
public static class PtpLightExport
{
    /// <summary>Light timelines of the project, in project order.</summary>
    public static IReadOnlyList<PtpTimeline> LightTimelines(PtpDocument document) =>
        document.Timelines.Where(t => t.Type == PtpTimeline.LightType).ToList();

    /// <param name="rate">Sampling step in milliseconds, PixelTuner 2's "discreteness" setting.</param>
    public static byte[] Export(IEnumerable<PtpTimeline> timelines, int rate = 5)
    {
        var lines = new List<LightExportTimeline>();
        foreach (var timeline in timelines)
        {
            if (timeline.Type != PtpTimeline.LightType || timeline.Items.Count == 0) continue;
            var line = new LightExportTimeline
            {
                IsMonochrome = timeline.Attributes.TryGetValue("IsMonochrome", out var mono)
                    && bool.TryParse(mono, out bool m) && m,
            };
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
    private static LightExportItem? Item(PtpItem item)
    {
        uint start = Milliseconds(item.Start), end = Milliseconds(item.End);
        if (item.Type == PtpItem.SolidType)
        {
            var color = ParseColor(item.Attributes.GetValueOrDefault("Color"));
            return new LightExportItem { Effect = LightEffect.Solid, StartMs = start, EndMs = end, ColorFirst = color, ColorLast = color };
        }
        if (item.Type != PtpItem.EffectType) return null;

        string id = item.Attributes.GetValueOrDefault("EffectID") ?? "";
        var effect = id switch
        {
            "Linear Transition" => LightEffect.LinearTransition,
            "Sine Wave" => LightEffect.SineWave,
            "Square Wave" => LightEffect.SquareWave,
            "Saw-tooth Wave" => LightEffect.SawToothWave,
            _ => throw new InvalidDataException($"Unknown light effect '{id}'."),
        };
        return new LightExportItem
        {
            Effect = effect,
            StartMs = start,
            EndMs = end,
            ColorFirst = ParseColor(item.Data.GetValueOrDefault("ColorFirst")),
            ColorLast = ParseColor(item.Data.GetValueOrDefault("ColorLast")),
            T1 = ParseInt(item.Data.GetValueOrDefault("T1")),
            T2 = ParseInt(item.Data.GetValueOrDefault("T2")),
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

    /// <summary>A WPF colour string as PixelTuner 2 stored it: #AARRGGBB, #RRGGBB, #ARGB or #RGB.</summary>
    internal static Rgb ParseColor(string? value)
    {
        if (value is null || value.Length < 4 || value[0] != '#'
            || !uint.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v))
            throw new InvalidDataException($"Bad colour '{value}'.");
        return (value.Length - 1) switch
        {
            8 or 6 => new Rgb((byte)(v >> 16), (byte)(v >> 8), (byte)v),
            4 or 3 => new Rgb((byte)(((v >> 8) & 0xf) * 17), (byte)(((v >> 4) & 0xf) * 17), (byte)((v & 0xf) * 17)),
            _ => throw new InvalidDataException($"Bad colour '{value}'."),
        };
    }

    private static int ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

    private static uint Milliseconds(TimeSpan t) => checked((uint)Math.Round(t.TotalMilliseconds));
}
