using PixelTuner.Core.Imaging;

namespace PixelTuner.Core.Export;

/// <summary>
/// One timeline of a pixel device (poi, staff, fan, hoop column) ready for export:
/// a gap-free run of images from time 0.
/// </summary>
public sealed class PixelExportTimeline
{
    /// <summary>Colour the device shows for this timeline in its menu (already scaled down).</summary>
    public Rgb Color { get; init; } = new(0, 0, 20);

    /// <summary>Colour the device shows when this timeline is selected.</summary>
    public Rgb SelectionColor { get; init; } = new(70, 0, 0);

    public List<PixelExportItem> Items { get; } = [];
}

/// <summary>One image on a timeline. The device shows its columns one after another.</summary>
public sealed class PixelExportItem
{
    /// <summary>
    /// Identifies the image so that the same picture is stored once in the .bin.
    /// Items with equal keys, brightness and flip share their pixel data. Null means
    /// "never share" (PixelTuner 2 gap fillers).
    /// </summary>
    public string? ImageKey { get; init; }

    public required PixelImage Image { get; init; }

    /// <summary>Start in milliseconds from the start of the timeline.</summary>
    public uint StartMs { get; init; }

    public uint DurationMs { get; init; }

    /// <summary>How many times the device repeats each column (speed of the picture).</summary>
    public ushort Stretching { get; init; } = 1;

    /// <summary>Brightness in percent, 0 to 100.</summary>
    public byte Brightness { get; init; } = 100;

    public bool LaserOn { get; init; }

    public bool FlipVertical { get; init; }
}
