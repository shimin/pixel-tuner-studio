namespace PixelTuner.Package;

public sealed class Manifest
{
    /// <summary>Version of the package layout and project schema.</summary>
    public int FormatVersion { get; set; } = PxPackageFormat.CurrentVersion;

    /// <summary>Oldest reader version that can open this package.</summary>
    public int MinReaderVersion { get; set; } = 1;

    public string Generator { get; set; } = "Pixel Tuner";
    public string? GeneratorVersion { get; set; }

    public Guid ProjectId { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset ModifiedUtc { get; set; }

    public List<ResourceEntry> Resources { get; set; } = [];
}

/// <summary>A media file inside the package.</summary>
public sealed class ResourceEntry
{
    /// <summary>Lowercase hex SHA-256 of the content. Also the resource id used by the project.</summary>
    public string Id { get; set; } = "";

    /// <summary>Path inside the archive, for example media/audio/&lt;id&gt;.mp3.</summary>
    public string Path { get; set; } = "";

    /// <summary>File name the user added, shown in the UI.</summary>
    public string OriginalName { get; set; } = "";

    public long Size { get; set; }
}
