namespace PixelTuner.Package;

/// <summary>
/// Layout of a .pxpkg file (a ZIP archive):
/// <code>
/// mimetype                      first entry, stored: application/vnd.pixeltuner.package+zip
/// manifest.json                 format version, app, project id, resource list with hashes
/// project.json                  the project (ShowProject)
/// media/audio/&lt;sha256&gt;.mp3     media by content hash, stored without recompression
/// media/images/&lt;sha256&gt;.png
/// media/video/&lt;sha256&gt;.mp4
/// cache/...                     regenerable data (waveform, thumbnails, previews)
/// export/&lt;device&gt;.bin          compiled device files (optional)
/// </code>
/// </summary>
public static class PxPackageFormat
{
    public const string Extension = ".pxpkg";
    public const string MimeType = "application/vnd.pixeltuner.package+zip";

    /// <summary>Version this build writes.</summary>
    public const int CurrentVersion = 1;

    public const string MimeTypeEntry = "mimetype";
    public const string ManifestEntry = "manifest.json";
    public const string ProjectEntry = "project.json";
    public const string MediaFolder = "media/";
    public const string CacheFolder = "cache/";
    public const string ExportFolder = "export/";

    /// <summary>Formats that are already compressed: stored as is, so they can be read without inflating.</summary>
    static readonly HashSet<string> Precompressed = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".aac", ".m4a", ".ogg", ".opus", ".flac",
        ".jpg", ".jpeg", ".png", ".gif", ".webp",
        ".mp4", ".mov", ".m4v", ".webm", ".avi",
        ".zip",
    };

    public static bool IsPrecompressed(string extension) => Precompressed.Contains(extension);

    /// <summary>Folder under media/ for a file extension.</summary>
    public static string MediaKind(string extension) => extension.ToLowerInvariant() switch
    {
        ".mp3" or ".wav" or ".aac" or ".m4a" or ".ogg" or ".opus" or ".flac" => "audio",
        ".mp4" or ".mov" or ".m4v" or ".webm" or ".avi" or ".mpg" or ".mpeg" => "video",
        ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp" or ".svg" => "images",
        _ => "other",
    };
}
