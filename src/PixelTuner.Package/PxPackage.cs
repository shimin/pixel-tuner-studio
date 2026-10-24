using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PixelTuner.Core.Media;
using PixelTuner.Core.Model;

namespace PixelTuner.Package;

/// <summary>An opened .pxpkg. Media is read on demand straight from the archive.</summary>
public sealed class PxPackage : IDisposable, IMediaSource
{
    readonly ZipArchive _zip;
    readonly Dictionary<string, ResourceEntry> _byId;

    public Manifest Manifest { get; }
    public ShowProject Project { get; }

    PxPackage(ZipArchive zip, Manifest manifest, ShowProject project)
    {
        _zip = zip;
        Manifest = manifest;
        Project = project;
        _byId = manifest.Resources.ToDictionary(r => r.Id, StringComparer.Ordinal);
    }

    public static PxPackage Open(string path) => Open(File.OpenRead(path), leaveOpen: false);

    public static PxPackage Open(Stream stream, bool leaveOpen = false)
    {
        var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen);
        try
        {
            var mime = ReadText(zip, PxPackageFormat.MimeTypeEntry);
            if (mime != PxPackageFormat.MimeType)
                throw new InvalidDataException("This is not a Pixel Tuner Studio package.");

            var manifest = JsonSerializer.Deserialize(ReadBytes(zip, PxPackageFormat.ManifestEntry), PackageJsonContext.Default.Manifest)
                ?? throw new InvalidDataException("The package manifest is empty.");

            if (manifest.MinReaderVersion > PxPackageFormat.CurrentVersion)
                throw new PackageTooNewException(manifest.FormatVersion, manifest.MinReaderVersion);

            var project = JsonSerializer.Deserialize(ReadBytes(zip, PxPackageFormat.ProjectEntry), PackageJsonContext.Default.ShowProject)
                ?? throw new InvalidDataException("The project is empty.");

            return new PxPackage(zip, manifest, project);
        }
        catch
        {
            zip.Dispose();
            throw;
        }
    }

    public bool HasResource(string id) => _byId.ContainsKey(id);

    public ResourceEntry GetResource(string id) =>
        _byId.TryGetValue(id, out var r) ? r : throw new KeyNotFoundException($"Resource {id} is not in the package.");

    /// <summary>Opens a media stream. Not thread-safe: open one at a time per package.</summary>
    public Stream OpenResource(string id)
    {
        var entry = _zip.GetEntry(GetResource(id).Path)
            ?? throw new InvalidDataException($"Resource {id} is listed in the manifest but missing from the archive.");
        return entry.Open();
    }

    Stream IMediaSource.Open(string resourceId) => OpenResource(resourceId);

    public byte[] ReadResource(string id)
    {
        using var s = OpenResource(id);
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>Checks every resource against its hash. Returns the ids that do not match.</summary>
    public IReadOnlyList<string> VerifyResources()
    {
        var broken = new List<string>();
        foreach (var r in Manifest.Resources)
        {
            var entry = _zip.GetEntry(r.Path);
            if (entry is null)
            {
                broken.Add(r.Id);
                continue;
            }
            using var s = entry.Open();
            if (Convert.ToHexStringLower(SHA256.HashData(s)) != r.Id)
                broken.Add(r.Id);
        }
        return broken;
    }

    /// <summary>Reads an optional cache/ or export/ entry; null when absent.</summary>
    public byte[]? TryReadEntry(string path)
    {
        var entry = _zip.GetEntry(path);
        if (entry is null) return null;
        using var s = entry.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    public void Dispose() => _zip.Dispose();

    static byte[] ReadBytes(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name) ?? throw new InvalidDataException($"The package has no {name}.");
        using var s = entry.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    static string ReadText(ZipArchive zip, string name) => Encoding.ASCII.GetString(ReadBytes(zip, name)).Trim();
}

public sealed class PackageTooNewException(int formatVersion, int minReaderVersion)
    : Exception($"The package needs a newer Pixel Tuner Studio (format {formatVersion}, reader {minReaderVersion} or later).")
{
    public int FormatVersion { get; } = formatVersion;
    public int MinReaderVersion { get; } = minReaderVersion;
}
