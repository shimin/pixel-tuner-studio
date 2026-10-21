namespace PixelTuner.Core.Model;

/// <summary>
/// A show project: soundtrack, timelines and devices. This is the stored model,
/// separate from view models, so it serializes the same way on every platform.
///
/// Draft for format version 1. It grows in phase 1 as timelines, effects and
/// LED markup move over from PixelTuner 2.
/// </summary>
public sealed class ShowProject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";

    /// <summary>Soundtrack the show is synced to. Null for a show without sound.</summary>
    public string? AudioResourceId { get; set; }

    public List<Timeline> Timelines { get; set; } = [];
    public List<DeviceBinding> Devices { get; set; } = [];

    /// <summary>Named points on the soundtrack for quick jumps on stage.</summary>
    public List<Marker> Markers { get; set; } = [];
}

public sealed class Timeline
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public List<TimelineItem> Items { get; set; } = [];
}

public sealed class TimelineItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Kind of item: "image", "video", "gradient", "solid", "effect".</summary>
    public string Kind { get; set; } = "";

    public TimeSpan Start { get; set; }
    public TimeSpan Duration { get; set; }

    /// <summary>Media inside the package (images, GIF, video), by resource id.</summary>
    public string? ResourceId { get; set; }

    /// <summary>Item-specific settings (effect parameters, colours and so on).</summary>
    public Dictionary<string, string> Parameters { get; set; } = [];
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
