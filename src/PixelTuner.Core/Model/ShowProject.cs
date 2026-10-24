namespace PixelTuner.Core.Model;

/// <summary>
/// A show project: soundtrack, timelines, LED markup and devices. This is the stored model,
/// separate from view models, so it serializes the same way on every platform.
/// Everything PixelTuner 2 kept in a .ptp has a place here, so an imported show exports
/// the same .bin as PixelTuner 2 did.
/// </summary>
public sealed class ShowProject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";

    /// <summary>Soundtrack the show is synced to. Null for a show without sound.</summary>
    public string? AudioResourceId { get; set; }

    /// <summary>Path of the soundtrack the show was made with, kept when the file was not found.</summary>
    public string? AudioSourceName { get; set; }

    public List<Timeline> Timelines { get; set; } = [];

    /// <summary>The drawing of the devices and their LED markup, used by video timelines.</summary>
    public ShowCanvas? Canvas { get; set; }

    public List<DeviceBinding> Devices { get; set; } = [];

    /// <summary>Named points on the soundtrack for quick jumps on stage.</summary>
    public List<Marker> Markers { get; set; } = [];

    /// <summary>Settings of PixelTuner 2 with no place of their own yet, by their .ptp names.</summary>
    public Dictionary<string, string>? Extra { get; set; }
}

/// <summary>Kinds of timeline: which devices can play it.</summary>
public static class TimelineKind
{
    /// <summary>Pixel devices: poi, staves, hoops, fans. Items are images, one column at a time.</summary>
    public const string Pixel = "pixel";

    /// <summary>LED matrices, shields and suits. Items are GIFs sampled through the LED markup.</summary>
    public const string Video = "video";

    /// <summary>E-Pic poi. Items are gradients: the top row of a picture.</summary>
    public const string Epic = "epic-poi";

    /// <summary>LightMaster channels. Items are solid colours and effects.</summary>
    public const string Light = "light";
}

public sealed class Timeline
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";

    /// <summary>One of <see cref="TimelineKind"/>.</summary>
    public string Kind { get; set; } = TimelineKind.Pixel;

    /// <summary>Colour tag as #RRGGBB: the timeline's colour in the editor and in the device menu.</summary>
    public string Color { get; set; } = "#0000FF";

    /// <summary>Pictures are played upside down.</summary>
    public bool FlipVertical { get; set; }

    /// <summary>Light timelines: a single-colour channel that uses only the red value.</summary>
    public bool Monochrome { get; set; }

    /// <summary>Video timelines: the <see cref="LedLayout.Id"/> of the device outline it plays on.</summary>
    public string? LayoutId { get; set; }

    /// <summary>The timeline is shown in the preview.</summary>
    public bool PreviewVisible { get; set; } = true;

    public List<TimelineItem> Items { get; set; } = [];

    /// <summary>Settings of PixelTuner 2 with no place of their own yet, by their .ptp names.</summary>
    public Dictionary<string, string>? Extra { get; set; }
}

/// <summary>Kinds of item on a timeline.</summary>
public static class ItemKind
{
    public const string Image = "image";
    public const string Video = "video";
    public const string Gradient = "gradient";
    public const string Solid = "solid";
    public const string Effect = "effect";
}

/// <summary>Light effects, by the names PixelTuner 2 used.</summary>
public static class EffectKind
{
    public const string LinearTransition = "Linear Transition";
    public const string SineWave = "Sine Wave";
    public const string SquareWave = "Square Wave";
    public const string SawToothWave = "Saw-tooth Wave";
}

public sealed class TimelineItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>One of <see cref="ItemKind"/>.</summary>
    public string Kind { get; set; } = "";

    public TimeSpan Start { get; set; }
    public TimeSpan Duration { get; set; }

    /// <summary>Media inside the package (image, GIF, gradient picture), by resource id.</summary>
    public string? ResourceId { get; set; }

    /// <summary>
    /// The file as the show named it (path and name). Kept to show the name and, when the
    /// file was not found, to find it again: then <see cref="ResourceId"/> is null.
    /// </summary>
    public string? SourceName { get; set; }

    /// <summary>How many times the device repeats each column (speed of the picture).</summary>
    public int Stretching { get; set; } = 1;

    /// <summary>Brightness in percent, 0 to 100.</summary>
    public int Brightness { get; set; } = 100;

    public bool LaserOn { get; set; }

    /// <summary>Video items: shift of the picture against the LED markup, in pixels.</summary>
    public int? OffsetX { get; set; }
    public int? OffsetY { get; set; }

    /// <summary>Solid items: the colour; effects: the first colour. #RRGGBB.</summary>
    public string? Color { get; set; }

    /// <summary>Effects: the second colour, #RRGGBB.</summary>
    public string? ColorLast { get; set; }

    /// <summary>Effects: one of <see cref="EffectKind"/>.</summary>
    public string? Effect { get; set; }

    /// <summary>Effects: periods in milliseconds (T1, T2 of PixelTuner 2).</summary>
    public int? T1 { get; set; }
    public int? T2 { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public TimeSpan End => Start + Duration;

    /// <summary>Settings of PixelTuner 2 with no place of their own yet, by their .ptp names.</summary>
    public Dictionary<string, string>? Extra { get; set; }
}

/// <summary>The device drawing (SVG) with LED markup on its outlines.</summary>
public sealed class ShowCanvas
{
    /// <summary>The SVG drawing inside the package, by resource id.</summary>
    public string? DrawingResourceId { get; set; }

    public List<LedLayout> Layouts { get; set; } = [];
}

/// <summary>
/// LED markup of one device outline: a grid of tiles, some of them LEDs with a number in the
/// order the device wires them.
/// </summary>
public sealed class LedLayout
{
    /// <summary>Id of the outline in the drawing (PixelTuner 2's element path).</summary>
    public string Id { get; set; } = "";

    public int Columns { get; set; }
    public int Rows { get; set; }
    public int TileMargin { get; set; }

    public List<LedTile> Tiles { get; set; } = [];

    /// <summary>
    /// Grid positions (column, row) of the LEDs in device order: selected tiles sorted by
    /// number, ties kept in tile order, as PixelTuner 2's OrderedLeds.
    /// </summary>
    public IReadOnlyList<(int X, int Y)> OrderedLeds()
    {
        if (Columns <= 0) return [];
        return Tiles.OrderBy(t => t.Number)            // OrderBy is stable, like LINQ in PixelTuner 2
            .Where(t => t.Selected)
            .Select(t => (t.Index % Columns, t.Index / Columns))
            .ToList();
    }
}

public sealed class LedTile
{
    /// <summary>Position in the grid, row by row.</summary>
    public int Index { get; set; }

    /// <summary>Order of the LED on the device's wire.</summary>
    public int Number { get; set; }

    /// <summary>The tile is an LED.</summary>
    public bool Selected { get; set; }
}

/// <summary>Which device plays which timeline.</summary>
public sealed class DeviceBinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";

    /// <summary>Device type as in PixelTuner 2: "pixel", "hoop", "epic-poi", "illuminator", "video".</summary>
    public string DeviceType { get; set; } = "";

    /// <summary>Name the device reports over the network (the lookup "tag").</summary>
    public string? NetworkTag { get; set; }

    public Guid? TimelineId { get; set; }
}

public sealed class Marker
{
    public string Name { get; set; } = "";
    public TimeSpan Position { get; set; }
}
