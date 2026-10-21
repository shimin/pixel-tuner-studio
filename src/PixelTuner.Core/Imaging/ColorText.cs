using System.Globalization;

namespace PixelTuner.Core.Imaging;

/// <summary>Colours as text, the way WPF wrote them into PixelTuner 2 projects.</summary>
public static class ColorText
{
    /// <summary>PixelTuner 2's default timeline colour tag (blue).</summary>
    public const string DefaultTag = "#FF0000FF";

    /// <summary>"#AARRGGBB"; anything else is PixelTuner 2's default blue.</summary>
    public static (byte A, byte R, byte G, byte B) ParseArgb(string? value)
    {
        if (value is { Length: 9 } && value[0] == '#'
            && uint.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb))
            return ((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
        return (255, 0, 0, 255);
    }

    public static string FormatArgb((byte A, byte R, byte G, byte B) c) =>
        string.Create(CultureInfo.InvariantCulture, $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}");

    /// <summary>#AARRGGBB, #RRGGBB, #ARGB or #RGB; alpha is ignored. Throws on anything else.</summary>
    public static Rgb ParseRgb(string? value)
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
}
