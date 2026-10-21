namespace PixelTuner.Core.Model;

/// <summary>Why a library entry cannot be put on a timeline.</summary>
public enum PlacementProblem
{
    None,

    /// <summary>The timeline plays another kind of thing (a picture on a light timeline).</summary>
    WrongKind,

    /// <summary>A pixel timeline plays pictures of another height.</summary>
    WrongHeight,

    /// <summary>The file of the entry is missing.</summary>
    MissingMedia,
}

/// <summary>
/// The rules of putting library entries on timelines, and the defaults a new item gets. What is allowed here is what
/// the .bin can be built from: the library never offers an entry that a timeline cannot play.
/// </summary>
public static class Library
{
    /// <summary>PixelTuner 2's defaults: 4 intervals of 50 µs per column for a picture, 40 ms per frame (25 a second) for a video.</summary>
    public const int DefaultPictureStretching = 4;

    public const int DefaultFrameTime = 40;

    /// <summary>The kind of timeline that plays an entry of this kind.</summary>
    public static string TimelineKindOf(string libraryKind) => libraryKind switch
    {
        LibraryKind.Image => TimelineKind.Pixel,
        LibraryKind.Video => TimelineKind.Video,
        LibraryKind.Gradient => TimelineKind.EpicPoi,
        LibraryKind.Color or LibraryKind.Effect => TimelineKind.Light,
        _ => throw new ArgumentOutOfRangeException(nameof(libraryKind)),
    };

    public static bool HasMedia(string libraryKind) => libraryKind is LibraryKind.Image or LibraryKind.Video or LibraryKind.Gradient;

    /// <param name="pixelHeight">The height pictures must have on the timeline (0: any); see <see cref="Editing.Edits.PixelHeight"/>.</param>
    public static PlacementProblem CanPlace(LibraryEntry entry, Timeline timeline)
    {
        if (TimelineKindOf(entry.Kind) != timeline.Kind) return PlacementProblem.WrongKind;
        if (HasMedia(entry.Kind) && entry.ResourceId is null) return PlacementProblem.MissingMedia;
        if (entry.Kind == LibraryKind.Image && !Editing.Edits.FitsHeight(timeline, entry.Height)) return PlacementProblem.WrongHeight;
        return PlacementProblem.None;
    }

    /// <summary>
    /// A new item for the timeline from an entry, with the defaults of its kind; the caller sets its start and length.
    /// </summary>
    /// <exception cref="InvalidOperationException">The entry cannot be put on this timeline (see <see cref="CanPlace"/>).</exception>
    public static TimelineItem NewItem(LibraryEntry entry, Timeline timeline)
    {
        if (CanPlace(entry, timeline) != PlacementProblem.None)
            throw new InvalidOperationException($"'{entry.Name}' cannot be put on this timeline.");
        return entry.Kind switch
        {
            LibraryKind.Color => new TimelineItem { Kind = ItemKind.Solid, Color = entry.Color },
            LibraryKind.Effect => new TimelineItem
            {
                Kind = ItemKind.Effect, Color = entry.Color, ColorLast = entry.ColorLast, Effect = entry.Effect, T1 = entry.T1, T2 = entry.T2,
            },
            _ => new TimelineItem
            {
                Kind = entry.Kind switch { LibraryKind.Video => ItemKind.Video, LibraryKind.Gradient => ItemKind.Gradient, _ => ItemKind.Image },
                ResourceId = entry.ResourceId,
                SourcePath = entry.SourcePath,
                MediaWidth = entry.Width,
                MediaHeight = entry.Height,
                Stretching = entry.Kind == LibraryKind.Video ? DefaultFrameTime : DefaultPictureStretching,
            },
        };
    }

    /// <summary>Whether items of the project play the file of this entry.</summary>
    public static bool IsUsed(ShowProject project, LibraryEntry entry) =>
        HasMedia(entry.Kind) && entry.ResourceId is { } id && project.Timelines.Any(t => t.Items.Any(i => i.ResourceId == id));

    /// <summary>
    /// Gives every picture, animation and gradient that an item plays an entry of the library, for projects from before the
    /// library existed (older packages, imported PixelTuner 2 projects). Existing entries are left alone.
    /// </summary>
    /// <returns>The number of entries added.</returns>
    public static int AddMissingEntries(ShowProject project)
    {
        // A file is told by its resource; a file that is missing has none, and is told by the path it was added from.
        static string? KeyOf(string? resourceId, string? sourcePath) =>
            resourceId ?? (string.IsNullOrEmpty(sourcePath) ? null : "path:" + sourcePath.ToLowerInvariant());
        var known = project.Library.Where(e => HasMedia(e.Kind)).Select(e => KeyOf(e.ResourceId, e.SourcePath)).OfType<string>().ToHashSet(StringComparer.Ordinal);
        int added = 0;
        foreach (var item in project.Timelines.SelectMany(t => t.Items))
        {
            if (item.Kind is not (ItemKind.Image or ItemKind.Video or ItemKind.Gradient)) continue;
            string? key = KeyOf(item.ResourceId, item.SourcePath);
            if (key is null || !known.Add(key)) continue;
            project.Library.Add(new LibraryEntry
            {
                Name = NameOf(item.SourcePath, item.ResourceId ?? key),
                Kind = item.Kind switch { ItemKind.Video => LibraryKind.Video, ItemKind.Gradient => LibraryKind.Gradient, _ => LibraryKind.Image },
                ResourceId = item.ResourceId,
                SourcePath = item.SourcePath,
                Width = item.MediaWidth,
                Height = item.MediaHeight,
            });
            added++;
        }
        return added;
    }

    private static string NameOf(string? sourcePath, string fallback)
    {
        if (string.IsNullOrEmpty(sourcePath)) return fallback[..Math.Min(8, fallback.Length)];
        string path = sourcePath.Replace('\\', '/');
        return path[(path.LastIndexOf('/') + 1)..];
    }
}
