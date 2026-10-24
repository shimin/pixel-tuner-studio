using System.Globalization;
using PixelTuner.Core.Imaging;

namespace PixelTuner.Core.Model;

/// <summary>Colours in the project are text: #RRGGBB.</summary>
public static class ColorText
{
    public static string Format(Rgb c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>Reads #RRGGBB, and also #AARRGGBB, #RGB and #ARGB as WPF wrote them (alpha is dropped).</summary>
    public static Rgb Parse(string? value)
    {
        if (value is null || value.Length < 4 || value[0] != '#'
            || !uint.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v))
            throw new FormatException($"Bad colour '{value}'.");
        return (value.Length - 1) switch
        {
            8 or 6 => new Rgb((byte)(v >> 16), (byte)(v >> 8), (byte)v),
            4 or 3 => new Rgb((byte)(((v >> 8) & 0xf) * 17), (byte)(((v >> 4) & 0xf) * 17), (byte)((v & 0xf) * 17)),
            _ => throw new FormatException($"Bad colour '{value}'."),
        };
    }
}
