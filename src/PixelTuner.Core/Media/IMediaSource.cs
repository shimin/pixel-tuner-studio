using System.Security.Cryptography;

namespace PixelTuner.Core.Media;

/// <summary>
/// Where the media of a show comes from, by resource id (the lowercase hex SHA-256 of the
/// content): an opened .pxpkg, or files on disk while a show is being imported.
/// </summary>
public interface IMediaSource
{
    /// <summary>Opens the content of a resource. Throws when the source has no such resource.</summary>
    Stream Open(string resourceId);
}

public static class MediaSourceExtensions
{
    public static byte[] ReadAll(this IMediaSource source, string resourceId)
    {
        using var stream = source.Open(resourceId);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}

/// <summary>
/// Media collected for a show before it is packed: files on disk and content made in memory
/// (for example the canvas drawing of an imported show), by resource id.
/// </summary>
public sealed class MediaCollection : IMediaSource
{
    private readonly Dictionary<string, MediaEntry> _entries = new(StringComparer.Ordinal);

    /// <summary>The media in the order it was added.</summary>
    public IReadOnlyCollection<MediaEntry> Entries => _entries.Values;

    /// <summary>Adds a file and returns its resource id. The same content gets the same id.</summary>
    public string AddFile(string path)
    {
        string id;
        using (var stream = File.OpenRead(path))
            id = Convert.ToHexStringLower(SHA256.HashData(stream));
        _entries.TryAdd(id, new MediaEntry(id, Path.GetFileName(path), path, null));
        return id;
    }

    /// <summary>Adds content from memory and returns its resource id.</summary>
    public string AddContent(byte[] content, string name)
    {
        string id = Convert.ToHexStringLower(SHA256.HashData(content));
        _entries.TryAdd(id, new MediaEntry(id, name, null, content));
        return id;
    }

    public Stream Open(string resourceId) =>
        _entries.TryGetValue(resourceId, out var entry)
            ? entry.FilePath is { } path ? File.OpenRead(path) : new MemoryStream(entry.Content!, writable: false)
            : throw new KeyNotFoundException($"No media with id {resourceId}.");
}

/// <param name="Name">File name shown to the user.</param>
/// <param name="FilePath">The file that holds the content, or null for content in memory.</param>
public sealed record MediaEntry(string Id, string Name, string? FilePath, byte[]? Content);
