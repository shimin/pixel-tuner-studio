using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PixelTuner.Core.Model;

namespace PixelTuner.Package;

/// <summary>
/// Builds a .pxpkg. Media is added by content: the same file added twice is stored once.
/// Saving is atomic: the package is written to a temporary file next to the target
/// and then moved over it, so a crash never leaves a half-written project.
/// </summary>
public sealed class PxPackageWriter
{
    readonly Dictionary<string, PendingResource> _resources = new(StringComparer.Ordinal);
    readonly Dictionary<string, byte[]> _extraEntries = new(StringComparer.Ordinal);

    /// <summary>Adds a media file and returns its resource id (SHA-256 hex).</summary>
    public string AddMediaFile(string filePath)
    {
        var id = HashFile(filePath);
        if (!_resources.ContainsKey(id))
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var entryPath = $"{PxPackageFormat.MediaFolder}{PxPackageFormat.MediaKind(ext)}/{id}{ext}";
            _resources[id] = new PendingResource(id, entryPath, Path.GetFileName(filePath), filePath, null);
        }
        return id;
    }

    /// <summary>Adds media from memory (for example, a resource copied from another package).</summary>
    public string AddMedia(byte[] content, string originalName)
    {
        var id = Convert.ToHexStringLower(SHA256.HashData(content));
        if (!_resources.ContainsKey(id))
        {
            var ext = Path.GetExtension(originalName).ToLowerInvariant();
            var entryPath = $"{PxPackageFormat.MediaFolder}{PxPackageFormat.MediaKind(ext)}/{id}{ext}";
            _resources[id] = new PendingResource(id, entryPath, originalName, null, content);
        }
        return id;
    }

    /// <summary>Adds a regenerable cache entry or a compiled export under cache/ or export/.</summary>
    public void AddEntry(string path, byte[] content)
    {
        if (!path.StartsWith(PxPackageFormat.CacheFolder, StringComparison.Ordinal) &&
            !path.StartsWith(PxPackageFormat.ExportFolder, StringComparison.Ordinal))
            throw new ArgumentException("Extra entries go under cache/ or export/.", nameof(path));
        _extraEntries[path] = content;
    }

    public void Save(string targetPath, ShowProject project, DateTimeOffset? createdUtc = null)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(targetPath))!;
        Directory.CreateDirectory(dir);
        var tempPath = Path.Combine(dir, $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write))
                Write(stream, project, createdUtc);
            File.Move(tempPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    public void Write(Stream output, ShowProject project, DateTimeOffset? createdUtc = null)
    {
        var now = DateTimeOffset.UtcNow;
        var manifest = new Manifest
        {
            ProjectId = project.Id,
            CreatedUtc = createdUtc ?? now,
            ModifiedUtc = now,
            GeneratorVersion = typeof(PxPackageWriter).Assembly.GetName().Version?.ToString(),
        };

        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);

        // mimetype goes first and uncompressed, so a file can be recognised by its first bytes.
        WriteEntry(zip, PxPackageFormat.MimeTypeEntry, Encoding.ASCII.GetBytes(PxPackageFormat.MimeType), CompressionLevel.NoCompression);

        foreach (var r in _resources.Values.OrderBy(r => r.EntryPath, StringComparer.Ordinal))
        {
            var level = PxPackageFormat.IsPrecompressed(Path.GetExtension(r.EntryPath))
                ? CompressionLevel.NoCompression
                : CompressionLevel.Optimal;
            var entry = zip.CreateEntry(r.EntryPath, level);
            long size;
            using (var target = entry.Open())
            {
                if (r.Content is { } bytes)
                {
                    target.Write(bytes);
                    size = bytes.Length;
                }
                else
                {
                    using var source = File.OpenRead(r.SourcePath!);
                    source.CopyTo(target);
                    size = source.Length;
                }
            }
            manifest.Resources.Add(new ResourceEntry { Id = r.Id, Path = r.EntryPath, OriginalName = r.OriginalName, Size = size });
        }

        foreach (var (path, content) in _extraEntries.OrderBy(e => e.Key, StringComparer.Ordinal))
            WriteEntry(zip, path, content, PxPackageFormat.IsPrecompressed(Path.GetExtension(path)) ? CompressionLevel.NoCompression : CompressionLevel.Optimal);

        WriteEntry(zip, PxPackageFormat.ProjectEntry, JsonSerializer.SerializeToUtf8Bytes(project, PackageJsonContext.Default.ShowProject), CompressionLevel.Optimal);
        WriteEntry(zip, PxPackageFormat.ManifestEntry, JsonSerializer.SerializeToUtf8Bytes(manifest, PackageJsonContext.Default.Manifest), CompressionLevel.Optimal);
    }

    static void WriteEntry(ZipArchive zip, string path, byte[] content, CompressionLevel level)
    {
        var entry = zip.CreateEntry(path, level);
        using var s = entry.Open();
        s.Write(content);
    }

    static string HashFile(string path)
    {
        using var s = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(s));
    }

    sealed record PendingResource(string Id, string EntryPath, string OriginalName, string? SourcePath, byte[]? Content);
}
