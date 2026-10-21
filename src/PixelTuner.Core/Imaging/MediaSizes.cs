using PixelTuner.Core.Model;

namespace PixelTuner.Core.Imaging;

/// <summary>
/// Fills in the picture size that the editor's rules need (<see cref="TimelineItem.MediaWidth"/> and
/// <see cref="TimelineItem.MediaHeight"/>) for items from projects that did not store it: an imported
/// PixelTuner 2 project, or a package from before the size was kept.
/// </summary>
public static class MediaSizes
{
    /// <param name="open">Opens a media file by resource id; null when the project does not have it.</param>
    /// <returns>The number of items whose size was filled in.</returns>
    public static int Fill(ShowProject project, Func<string, Stream?> open)
    {
        var sizes = new Dictionary<string, (int Width, int Height)?>(StringComparer.Ordinal);
        int filled = 0;
        foreach (var item in project.Timelines.SelectMany(t => t.Items))
        {
            if (item.ResourceId is not { } id || item.MediaHeight > 0 || item.Kind is ItemKind.Solid or ItemKind.Effect) continue;
            if (!sizes.TryGetValue(id, out var size))
                sizes[id] = size = Read(open, id);
            if (size is not { } known) continue;
            (item.MediaWidth, item.MediaHeight) = known;
            filled++;
        }
        return filled;
    }

    private static (int, int)? Read(Func<string, Stream?> open, string id)
    {
        try
        {
            using var stream = open(id);
            return stream is null ? null : ImageDecoder.ReadSize(stream);
        }
        catch (InvalidDataException)
        {
            return null;   // a damaged file stays "unknown"; the export reports it
        }
    }
}
