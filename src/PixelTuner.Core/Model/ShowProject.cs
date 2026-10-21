using System.Text.Json.Serialization;
using PixelTuner.Core.Export;

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

    /// <summary>How the .bin is built from this project; saved with it, so every computer builds the same file.</summary>
    public BuildSettings Build { get; set; } = new();

    /// <summary>What can be put on the timelines: the pictures, animations and gradients of the project, and colours and effects for light.</summary>
    public List<LibraryEntry> Library { get; set; } = [];

    public List<Timeline> Timelines { get; set; } = [];

    /// <summary>LED markup of the props on the canvas; video timelines refer to one each.</summary>
    public List<LedLayout> LedLayouts { get; set; } = [];

    public List<DeviceBinding> Devices { get; set; } = [];

    /// <summary>Named points on the soundtrack for quick jumps on stage.</summary>
    public List<Marker> Markers { get; set; } = [];
}

/// <summary>
/// The parameters of the .bin that are not on the timelines. PixelTuner 2 kept them in the program's settings,
/// so the same .ptp could give different files on two computers; here they belong to the project, with PixelTuner 2's
/// defaults.
/// </summary>
public sealed class BuildSettings
{
    public const int DefaultGapStretching = 4;
    public const int DefaultLightRate = 5;

    /// <summary>Bits per colour in the picture blocks: 12, 24 (what current devices play) or 36.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ColorDepth>))]
    public ColorDepth Depth { get; set; } = ColorDepth.Bit24;

    /// <summary>Stretching of the black entries that fill the pauses between items (PixelTuner 2's "default stretching").</summary>
    public int GapStretching { get; set; } = DefaultGapStretching;

    /// <summary>LightMaster: milliseconds between samples (PixelTuner 2's "discreteness").</summary>
    public int LightRate { get; set; } = DefaultLightRate;
}

/// <summary>Kinds of library entries; each is made for one kind of timeline.</summary>
public static class LibraryKind
{
    /// <summary>A picture: its columns are played by a pixel timeline of the picture's height.</summary>
    public const string Image = "image";

    /// <summary>A GIF for a video timeline.</summary>
    public const string Video = "video";

    /// <summary>A gradient picture for an E-Pic timeline (it plays the top row).</summary>
    public const string Gradient = "gradient";

    /// <summary>A colour for a light timeline.</summary>
    public const string Color = "color";

    /// <summary>A light effect (colours, kind and times) for a light timeline.</summary>
    public const string Effect = "effect";
}

/// <summary>One thing of the project's library. Media entries refer to a file in the package; colours and effects carry their values.</summary>
public sealed class LibraryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";

    /// <summary>One of <see cref="LibraryKind"/>.</summary>
    public string Kind { get; set; } = LibraryKind.Image;

    /// <summary>Media entries: the file in the package. Null when the file is missing.</summary>
    public string? ResourceId { get; set; }

    /// <summary>Media entries: where the file came from.</summary>
    public string? SourcePath { get; set; }

    /// <summary>Media entries: size in pixels (the first frame of a GIF); a picture's height is the number of LEDs it needs.</summary>
    public int Width { get; set; }

    public int Height { get; set; }

    /// <summary>GIF entries: the number of frames; 0 while unknown.</summary>
    public int Frames { get; set; }

    /// <summary>Colour entries: the colour; effect entries: the first colour. #RRGGBB.</summary>
    public string? Color { get; set; }

    /// <summary>Effect entries: the colour the effect goes to.</summary>
    public string? ColorLast { get; set; }

    /// <summary>Effect entries: one of <see cref="LightEffectKind"/>.</summary>
    public string? Effect { get; set; }

    public int T1 { get; set; }
    public int T2 { get; set; }
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

    /// <summary>
    /// Pixel timelines: the number of LEDs of the device that plays it, which is the height of every picture on it
    /// (H in the .bin). 0 while the timeline takes the height of its first picture.
    /// </summary>
    public int Height { get; set; }

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
    /// Size of the picture in pixels (the first frame of a GIF), set when the media is added. 0 while unknown,
    /// as in a package from before the size was stored; the rules that need it then do not apply to the item.
    /// </summary>
    public int MediaWidth { get; set; }

    /// <summary>See <see cref="MediaWidth"/>. For a picture on a pixel timeline this is the number of LEDs it needs.</summary>
    public int MediaHeight { get; set; }

    /// <summary>
    /// Path the media was added from, as PixelTuner 2 stored it (relative or absolute).
    /// Names a missing file, and keeps PixelTuner 2's export: it shared one image
    /// between items by this path, not by content.
    /// </summary>
    public string? SourcePath { get; set; }

    /// <summary>
    /// The speed of an image on the device: how long each column is held. Pixel devices and hoops count 50 µs
    /// intervals (the same .bin), video devices count milliseconds per frame (40 is 25 frames a second).
    /// The item's time span only says how long it plays. PixelTuner 2's defaults: 4 for pictures, 40 for video.
    /// </summary>
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

/// <summary>The kinds of device a show is built for. Each takes its own .bin.</summary>
public static class DeviceKind
{
    /// <summary>Pixel Poi and staves: pixel timelines, k × 50 µs per column.</summary>
    public const string Poi = "poi";

    /// <summary>Pixel Hoop: pixel timelines, a column is the whole ring.</summary>
    public const string Hoop = "hoop";

    /// <summary>Fan on a video module: one pixel timeline, k milliseconds per column.</summary>
    public const string Fan = "fan";

    /// <summary>Video module: video timelines (one, or a pair of LED chains).</summary>
    public const string Video = "video";

    /// <summary>E-Pic poi: E-Pic timelines.</summary>
    public const string EpicPoi = "epic-poi";

    /// <summary>LightMaster: light timelines, one per channel.</summary>
    public const string Light = "light";
}

/// <summary>Which device plays which timelines, in which order: the .bin of a device is built from this list.</summary>
public sealed class DeviceBinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";

    /// <summary>One of <see cref="DeviceKind"/>.</summary>
    public string DeviceType { get; set; } = DeviceKind.Poi;

    /// <summary>Name the device reports over the network (the lookup "tag").</summary>
    public string? NetworkTag { get; set; }

    /// <summary>
    /// LEDs of the device (H in the .bin of a pixel device); the pixel timelines it plays must be that high.
    /// 0 while unknown (before the device was ever connected).
    /// </summary>
    public int LedCount { get; set; }

    /// <summary>Memory for shows in bytes; the .bin must fit. 0 while unknown.</summary>
    public long MemoryBytes { get; set; }

    /// <summary>
    /// Timelines the device plays, in the order of the .bin: for a poi that is the order of its menu programs,
    /// for the LightMaster the order of its channels.
    /// </summary>
    public List<Guid> TimelineIds { get; set; } = [];

    /// <summary>Packages from before a device could play several timelines stored one id; it is read into the list.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? TimelineId
    {
        get => null;
        set
        {
            if (value is { } id && !TimelineIds.Contains(id)) TimelineIds.Add(id);
        }
    }
}

public sealed class Marker
{
    public string Name { get; set; } = "";
    public TimeSpan Position { get; set; }
}
