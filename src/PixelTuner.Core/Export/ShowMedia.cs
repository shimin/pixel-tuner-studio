using PixelTuner.Core.Imaging;

namespace PixelTuner.Core.Export;

/// <summary>
/// Decoded media of a show for the .bin export, by resource id. Each file is decoded
/// once per instance.
/// </summary>
public sealed class ShowMedia(Func<string, Stream> open)
{
    private readonly Dictionary<string, PixelImage> _images = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<PixelImage>> _frames = new(StringComparer.Ordinal);

    /// <summary>Media of an imported PixelTuner 2 project before packaging: the resource id is the file path.</summary>
    public static ShowMedia FromFiles() => new(File.OpenRead);

    /// <summary>Opens the file of a resource; the caller disposes the stream. Throws when the project does not have it.</summary>
    internal Stream Open(string resourceId) => open(resourceId);

    /// <summary>The picture (the first frame of a GIF).</summary>
    public PixelImage Image(string resourceId)
    {
        if (!_images.TryGetValue(resourceId, out var image))
        {
            using var stream = open(resourceId);
            _images[resourceId] = image = ImageDecoder.Decode(stream);
        }
        return image;
    }

    /// <summary>Every GIF frame as stored in the file (see <see cref="GifFrames"/>).</summary>
    public IReadOnlyList<PixelImage> GifFrames(string resourceId)
    {
        if (!_frames.TryGetValue(resourceId, out var frames))
        {
            using var stream = open(resourceId);
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            _frames[resourceId] = frames = Imaging.GifFrames.Decode(copy.GetBuffer().AsSpan(0, (int)copy.Length));
        }
        return frames;
    }

    /// <summary>
    /// Key under which PixelTuner 2 shared one encoded image between items: the path the
    /// item was added from (lower case), not its content. The resource id keeps two
    /// different files that happen to have the same name apart.
    /// </summary>
    internal static string MediaKey(Model.TimelineItem item) =>
        $"{item.SourcePath?.ToLowerInvariant()}|{item.ResourceId}";

    internal static uint Milliseconds(TimeSpan t) => checked((uint)Math.Round(t.TotalMilliseconds));
}
