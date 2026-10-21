using System.Text.Json.Serialization;

namespace PixelTuner.Core.Model;

/// <summary>
/// A show project: soundtrack, timelines and devices. This is the stored model,
/// separate from view models, so it serializes the same way on every platform.
/// Media is referenced by resource id; the package (or, for an imported PixelTuner 2
/// project, the importer) knows where the bytes are.
/// </summary>
public sealed class ShowProject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";

    /// <summary>Soundtrack the show is synced to. Null for a show without sound.</summary>
    public string? AudioResourceId { get; set; }

    /// <summary>
    /// Where the soundtrack came from (PixelTuner 2's AudioPath). Kept when the file was
    /// missing on import, so the app can name it and ask for it again.
    /// </summary>
    public string? AudioSourcePath { get; set; }

    /// <summary>Drawing of the props (SVG) that LED layouts are marked up on.</summary>
    public string? CanvasResourceId { get; set; }

    public List<Timeline> Timelines { get; set; } = [];

    /// <summary>LED markup of the props on the canvas; video timelines refer to one each.</summary>
    public List<LedLayout> LedLayouts { get; set; } = [];

    public List<DeviceBinding> Devices { get; set; } = [];

    /// <summary>Named points on the soundtrack for quick jumps on stage.</summary>
    public List<Marker> Markers { get; set; } = [];
}

/// <summary>What a timeline drives. The value decides which .bin it compiles to.</summary>
public static class TimelineKind
{
    /// <summary>Poi, staves, hoops and fans: one image column per step.</summary>
    public const string Pixel = "pixel";

    /// <summary>LED matrices, shields and suits: GIF frames sampled at the LED layout.</summary>
    public const string Video = "video";

    /// <summary>E-Pic poi: the top row of a gradient picture.</summary>
    public const string EpicPoi = "epic-poi";

    /// <summary>LightMaster: solid colours and light effects.</summary>
    public const string Light = "light";
}

public sealed class Timeline
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";

    /// <summary>One of <see cref="TimelineKind"/>.</summary>
    public string Kind { get; set; } = TimelineKind.Pixel;

    /// <summary>
    /// Colour tag as #AARRGGBB. Devices show it in their menu (pixel devices get it / 10,
    /// E-Pic poi as is).
    /// </summary>
    public string Color { get; set; } = "#FF0000FF";

    /// <summary>Pixel and video timelines: images are played upside down.</summary>
    public bool FlipVertical { get; set; }

    /// <summary>Light timelines: three monochrome channels share one RGB output.</summary>
    public bool Monochrome { get; set; }

    /// <summary>Video timelines: the LED layout the frames are sampled at.</summary>
    public Guid? LedLayoutId { get; set; }

    public List<TimelineItem> Items { get; set; } = [];
}

/// <summary>Kinds of timeline items.</summary>
public static class ItemKind
{
    /// <summary>An image played column by column (pixel timelines).</summary>
    public const string Image = "image";

    /// <summary>A GIF whose frames are sampled at the LED layout (video timelines).</summary>
    public const string Video = "video";

    /// <summary>A gradient picture whose top row is played (E-Pic poi).</summary>
    public const string Gradient = "gradient";

    /// <summary>One colour (light timelines).</summary>
    public const string Solid = "solid";

    /// <summary>A light effect between two colours (light timelines).</summary>
    public const string Effect = "effect";
}

/// <summary>Light effects, as PixelTuner 2 named them.</summary>
public static class LightEffectKind
{
    public const string LinearTransition = "linear-transition";
    public const string SineWave = "sine-wave";
    public const string SquareWave = "square-wave";
    public const string SawToothWave = "saw-tooth-wave";
}

public sealed class TimelineItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>One of <see cref="ItemKind"/>.</summary>
    public string Kind { get; set; } = ItemKind.Image;

    public TimeSpan Start { get; set; }
    public TimeSpan Duration { get; set; }

    /// <summary>Media inside the package (images, GIF), by resource id. Null when the file is missing.</summary>
    public string? ResourceId { get; set; }

    /// <summary>
    /// Path the media was added from, as PixelTuner 2 stored it (relative or absolute).
    /// Names a missing file, and keeps PixelTuner 2's export: it shared one image
    /// between items by this path, not by content.
    /// </summary>
    public string? SourcePath { get; set; }

    /// <summary>How many times each image column is repeated on the device.</summary>
    public int Stretching { get; set; } = 1;

    /// <summary>Brightness in percent, 0–100.</summary>
    public byte Brightness { get; set; } = 100;

    public bool LaserOn { get; set; }

    /// <summary>Video items: where the LED layout sits on the frame, in pixels.</summary>
    public int OffsetX { get; set; }

    /// <summary>Video items: where the LED layout sits on the frame, in pixels.</summary>
    public int OffsetY { get; set; }

    /// <summary>Solid items: the colour; effect items: the first colour. #AARRGGBB or #RRGGBB.</summary>
    public string? Color { get; set; }

    /// <summary>Effect items: the colour the effect goes to.</summary>
    public string? ColorLast { get; set; }

    /// <summary>Effect items: one of <see cref="LightEffectKind"/>.</summary>
    public string? Effect { get; set; }

    /// <summary>Effect items: first time parameter in milliseconds (PixelTuner 2's T1).</summary>
    public int T1 { get; set; }

    /// <summary>Effect items: second time parameter in milliseconds (PixelTuner 2's T2).</summary>
    public int T2 { get; set; }

    [JsonIgnore]
    public TimeSpan End => Start + Duration;
}

/// <summary>
/// LED markup of a prop: a grid of tiles over its outline on the canvas, and the tiles
/// that are LEDs, in the order the device wires them.
/// </summary>
public sealed class LedLayout
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Id of the prop's outline in the canvas drawing (an SVG element id).</summary>
    public string Name { get; set; } = "";

    public int Columns { get; set; }
    public int Rows { get; set; }

    /// <summary>Tile index (row * Columns + column) of each LED, in wiring order.</summary>
    public List<int> Leds { get; set; } = [];
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
