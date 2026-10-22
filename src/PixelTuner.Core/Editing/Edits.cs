using PixelTuner.Core.Model;

namespace PixelTuner.Core.Editing;

/// <summary>
/// The changes the editor makes to a <see cref="ShowProject"/>. Each factory checks its arguments
/// and returns an <see cref="Edit"/> that is given to <see cref="UndoHistory.Do"/>; nothing
/// changes until then. Items on one timeline stay sorted by start and never overlap, because the
/// device export reads them in order and fills the gaps between them.
/// </summary>
public static class Edits
{
    // ---- project

    public static Edit RenameProject(ShowProject project, string name) =>
        new SetValueEdit<string>("RenameProject", project, nameof(project.Name), () => project.Name, v => project.Name = v, name, mergeable: true);

    /// <summary>Sets the soundtrack. The media itself is the document's business; this is the reference to it.</summary>
    public static Edit SetAudio(ShowProject project, string resourceId, string sourcePath) =>
        new GroupEdit("SetAudio",
        [
            new SetValueEdit<string?>("SetAudio", project, nameof(project.AudioResourceId), () => project.AudioResourceId, v => project.AudioResourceId = v, resourceId, mergeable: false),
            new SetValueEdit<string?>("SetAudio", project, nameof(project.AudioSourcePath), () => project.AudioSourcePath, v => project.AudioSourcePath = v, sourcePath, mergeable: false),
        ]);

    public static Edit RemoveAudio(ShowProject project) =>
        new GroupEdit("RemoveAudio",
        [
            new SetValueEdit<string?>("RemoveAudio", project, nameof(project.AudioResourceId), () => project.AudioResourceId, v => project.AudioResourceId = v, null, mergeable: false),
            new SetValueEdit<string?>("RemoveAudio", project, nameof(project.AudioSourcePath), () => project.AudioSourcePath, v => project.AudioSourcePath = v, null, mergeable: false),
        ]);

    // ---- build settings

    public static Edit SetColorDepth(ShowProject project, Export.ColorDepth depth)
    {
        if (!Enum.IsDefined(depth)) throw new ArgumentOutOfRangeException(nameof(depth));
        var settings = project.Build;
        return new SetValueEdit<Export.ColorDepth>("SetColorDepth", settings, nameof(settings.Depth), () => settings.Depth, v => settings.Depth = v, depth, mergeable: false);
    }

    /// <summary>Stretching of the pauses. At least <see cref="MinStretching"/>; at most what E-Pic can store (it multiplies by 5).</summary>
    public static Edit SetGapStretching(ShowProject project, int stretching)
    {
        if (stretching < MinStretching || stretching > MaxEpicStretching) throw new ArgumentOutOfRangeException(nameof(stretching));
        var settings = project.Build;
        return new SetValueEdit<int>("SetGapStretching", settings, nameof(settings.GapStretching), () => settings.GapStretching, v => settings.GapStretching = v, stretching, mergeable: true);
    }

    /// <summary>LightMaster sampling step in milliseconds.</summary>
    public static Edit SetLightRate(ShowProject project, int rate)
    {
        if (rate < 1 || rate > MaxLightRate) throw new ArgumentOutOfRangeException(nameof(rate));
        var settings = project.Build;
        return new SetValueEdit<int>("SetLightRate", settings, nameof(settings.LightRate), () => settings.LightRate, v => settings.LightRate = v, rate, mergeable: true);
    }

    /// <summary>
    /// The least stretching of any item: 2 (100 µs per column on a poi, 2 ms between frames on a video module).
    /// The editor holds to it for every kind of device, so the build has nothing to refuse.
    /// </summary>
    public const int MinStretching = 2;

    /// <summary>The most E-Pic can store: the file keeps the stretching × 5 in 16 bits.</summary>
    public const int MaxEpicStretching = ushort.MaxValue / 5;

    public const int MaxLightRate = 1000;

    /// <summary>Items and the pauses between them make at most 65535 entries of a timeline in the .bin.</summary>
    public const int MaxItemsPerTimeline = ushort.MaxValue / 2;

    // ---- timelines

    public static Edit AddTimeline(ShowProject project, Timeline timeline, int? index = null)
    {
        int at = index ?? project.Timelines.Count;
        if (at < 0 || at > project.Timelines.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (!IsKnownKind(timeline.Kind)) throw new ArgumentException($"Unknown timeline kind '{timeline.Kind}'.", nameof(timeline));
        return new InsertEdit<Timeline>("AddTimeline", project.Timelines, at, timeline);
    }

    public static Edit RemoveTimeline(ShowProject project, Timeline timeline)
    {
        var edits = new List<Edit>();
        // Devices that played this timeline stop playing it, and play it again with undo.
        foreach (var binding in project.Devices.Where(d => d.TimelineIds.Contains(timeline.Id)))
            edits.Add(new RemoveEdit<Guid>("RemoveTimeline", binding.TimelineIds, timeline.Id));
        edits.Add(new RemoveEdit<Timeline>("RemoveTimeline", project.Timelines, timeline));
        return new GroupEdit("RemoveTimeline", edits);
    }

    public static Edit RenameTimeline(Timeline timeline, string name) =>
        new SetValueEdit<string>("RenameTimeline", timeline, nameof(timeline.Name), () => timeline.Name, v => timeline.Name = v, name, mergeable: true);

    public static Edit MoveTimeline(ShowProject project, Timeline timeline, int newIndex)
    {
        int from = project.Timelines.IndexOf(timeline);
        if (from < 0) throw new InvalidOperationException("The timeline is not in the project.");
        if (newIndex < 0 || newIndex >= project.Timelines.Count) throw new ArgumentOutOfRangeException(nameof(newIndex));
        return new MoveEdit<Timeline>("MoveTimeline", project.Timelines, from, newIndex);
    }

    /// <param name="color">#AARRGGBB, as stored.</param>
    public static Edit SetTimelineColor(Timeline timeline, string color) =>
        new SetValueEdit<string>("SetTimelineColor", timeline, nameof(timeline.Color), () => timeline.Color, v => timeline.Color = v, color, mergeable: true);

    /// <summary>Plays the pictures of a pixel timeline upside down (the device is mounted the other way up).</summary>
    public static Edit SetFlipVertical(Timeline timeline, bool flip)
    {
        if (timeline.Kind != TimelineKind.Pixel) throw new InvalidOperationException("Only a pixel timeline can be flipped.");
        return new SetValueEdit<bool>("SetFlipVertical", timeline, nameof(timeline.FlipVertical), () => timeline.FlipVertical, v => timeline.FlipVertical = v, flip, mergeable: false);
    }

    /// <summary>A light timeline of a monochrome channel: three of them share one RGB output.</summary>
    public static Edit SetMonochrome(Timeline timeline, bool monochrome)
    {
        if (timeline.Kind != TimelineKind.Light) throw new InvalidOperationException("Only a light timeline can be monochrome.");
        return new SetValueEdit<bool>("SetMonochrome", timeline, nameof(timeline.Monochrome), () => timeline.Monochrome, v => timeline.Monochrome = v, monochrome, mergeable: false);
    }

    /// <summary>The markup of the props a video timeline samples its frames at; null takes it away.</summary>
    public static Edit SetLedLayout(ShowProject project, Timeline timeline, Guid? layoutId)
    {
        if (timeline.Kind != TimelineKind.Video) throw new InvalidOperationException("Only a video timeline has an LED layout.");
        if (layoutId is { } id && project.LedLayouts.All(l => l.Id != id)) throw new ArgumentException("The project has no such layout.", nameof(layoutId));
        return new SetValueEdit<Guid?>("SetLedLayout", timeline, nameof(timeline.LedLayoutId), () => timeline.LedLayoutId, v => timeline.LedLayoutId = v, layoutId, mergeable: false);
    }

    // ---- LED layouts

    /// <summary>Most cells a layout grid can have: the editor draws every one, and a video module has no use for more.</summary>
    public const int MaxLedGridCells = 4096;

    /// <param name="use">A video timeline that takes the new layout at once, in the same undo step.</param>
    public static Edit AddLedLayout(ShowProject project, LedLayout layout, Timeline? use = null)
    {
        if (project.LedLayouts.Any(l => l.Id == layout.Id)) throw new ArgumentException("The project has this layout already.", nameof(layout));
        CheckGrid(layout.Columns, layout.Rows);
        var add = new InsertEdit<LedLayout>("AddLedLayout", project.LedLayouts, project.LedLayouts.Count, layout);
        if (use is null) return add;
        if (use.Kind != TimelineKind.Video) throw new InvalidOperationException("Only a video timeline has an LED layout.");
        return new GroupEdit("AddLedLayout",
        [
            add,
            new SetValueEdit<Guid?>("AddLedLayout", use, nameof(use.LedLayoutId), () => use.LedLayoutId, v => use.LedLayoutId = v, layout.Id, mergeable: false),
        ]);
    }

    /// <summary>Takes a layout away. The timelines that sampled their frames at it are left without one, and undo gives it back to them.</summary>
    public static Edit RemoveLedLayout(ShowProject project, LedLayout layout)
    {
        var edits = new List<Edit>();
        foreach (var timeline in project.Timelines.Where(t => t.LedLayoutId == layout.Id))
        {
            var t = timeline;
            edits.Add(new SetValueEdit<Guid?>("RemoveLedLayout", t, nameof(t.LedLayoutId), () => t.LedLayoutId, v => t.LedLayoutId = v, null, mergeable: false));
        }
        edits.Add(new RemoveEdit<LedLayout>("RemoveLedLayout", project.LedLayouts, layout));
        return new GroupEdit("RemoveLedLayout", edits);
    }

    public static Edit RenameLedLayout(LedLayout layout, string name) =>
        new SetValueEdit<string>("RenameLedLayout", layout, nameof(layout.Name), () => layout.Name, v => layout.Name = v, name, mergeable: true);

    /// <summary>Most space between tiles the markup window allows (PixelTuner 2's range was 0 to 100).</summary>
    public const int MaxTilesMargin = 100;

    /// <summary>
    /// The grid and the LED chain of a layout, in one step; what the markup window hands back with OK. The numbers are 1..N from here on,
    /// so what a PixelTuner 2 file had wrong with them is forgotten.
    /// </summary>
    /// <param name="tilesMargin">The space between the tiles, or null to leave it.</param>
    public static Edit SetLedMarkup(LedLayout layout, Layout.LedMarkup markup, int? tilesMargin = null)
    {
        CheckGrid(markup.Columns, markup.Rows);
        var edits = new List<Edit>
        {
            new SetValueEdit<int>("SetLedMarkup", layout, nameof(layout.Columns), () => layout.Columns, v => layout.Columns = v, markup.Columns, mergeable: false),
            new SetValueEdit<int>("SetLedMarkup", layout, nameof(layout.Rows), () => layout.Rows, v => layout.Rows = v, markup.Rows, mergeable: false),
            new SetValueEdit<List<int>>("SetLedMarkup", layout, nameof(layout.Leds), () => layout.Leds, v => layout.Leds = v, [.. markup.Chain], mergeable: false),
        };
        if (tilesMargin is { } margin)
        {
            if (margin < 0 || margin > MaxTilesMargin) throw new ArgumentOutOfRangeException(nameof(tilesMargin));
            edits.Add(new SetValueEdit<int>("SetLedMarkup", layout, nameof(layout.TilesMargin), () => layout.TilesMargin, v => layout.TilesMargin = v, margin, mergeable: false));
        }
        if (layout.SourceIssues is not null)
            edits.Add(new SetValueEdit<Layout.LedNumberIssues?>("SetLedMarkup", layout, nameof(layout.SourceIssues), () => layout.SourceIssues, v => layout.SourceIssues = v, null, mergeable: false));
        return new GroupEdit("SetLedMarkup", edits);
    }

    /// <summary>The drawing of the props (an SVG in the package's media) the layouts are marked up on; null takes it away.</summary>
    public static Edit SetCanvas(ShowProject project, string? resourceId) =>
        new SetValueEdit<string?>("SetCanvas", project, nameof(project.CanvasResourceId), () => project.CanvasResourceId, v => project.CanvasResourceId = v, resourceId, mergeable: false);

    private static void CheckGrid(int columns, int rows)
    {
        if (columns < 1 || rows < 1 || (long)columns * rows > MaxLedGridCells)
            throw new ArgumentOutOfRangeException(nameof(columns), $"A layout grid has 1 to {MaxLedGridCells} cells.");
    }

    // ---- items

    /// <summary>
    /// Another file in place of the picture of an item, for a missing file or a better version. The new file must
    /// suit the timeline as a new item would: the same height on a pixel timeline.
    /// </summary>
    public static Edit ReplaceMedia(Timeline timeline, TimelineItem item, string resourceId, string sourcePath, int width, int height)
    {
        RequireIndex(timeline, item);
        RequireKind(item, "picture", ItemKind.Image, ItemKind.Video, ItemKind.Gradient);
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        // The item being replaced does not decide the height: it is the one that changes.
        int required = timeline.Kind != TimelineKind.Pixel ? 0
            : timeline.Height > 0 ? timeline.Height
            : timeline.Items.FirstOrDefault(i => !ReferenceEquals(i, item) && i.MediaHeight > 0)?.MediaHeight ?? 0;
        if (required != 0 && required != height)
            throw new InvalidOperationException($"The picture is {height} LEDs high, the timeline plays {required}.");
        return new GroupEdit("ReplaceMedia",
        [
            new SetValueEdit<string?>("ReplaceMedia", item, nameof(item.ResourceId), () => item.ResourceId, v => item.ResourceId = v, resourceId, mergeable: false),
            new SetValueEdit<string?>("ReplaceMedia", item, nameof(item.SourcePath), () => item.SourcePath, v => item.SourcePath = v, sourcePath, mergeable: false),
            new SetValueEdit<int>("ReplaceMedia", item, nameof(item.MediaWidth), () => item.MediaWidth, v => item.MediaWidth = v, width, mergeable: false),
            new SetValueEdit<int>("ReplaceMedia", item, nameof(item.MediaHeight), () => item.MediaHeight, v => item.MediaHeight = v, height, mergeable: false),
        ]);
    }

    /// <summary>
    /// The .bin stores times as whole milliseconds, so the editor keeps them whole: what the user sees
    /// on the timeline is exactly what the device plays.
    /// </summary>
    public static bool IsWholeMs(TimeSpan time) => time.Ticks % TimeSpan.TicksPerMillisecond == 0;

    /// <summary>The whole millisecond nearest to a time.</summary>
    public static TimeSpan WholeMs(TimeSpan time) =>
        TimeSpan.FromTicks((long)Math.Round((double)time.Ticks / TimeSpan.TicksPerMillisecond, MidpointRounding.AwayFromZero) * TimeSpan.TicksPerMillisecond);

    private static void RequireWholeMs(TimeSpan time, string name)
    {
        if (!IsWholeMs(time)) throw new ArgumentException($"{name} is not a whole number of milliseconds: {time}.", name);
    }

    /// <summary>Whether an item of this length fits at this start without touching its neighbours' time.</summary>
    /// <param name="ignore">The item being moved or resized, which does not block itself.</param>
    public static bool Fits(Timeline timeline, TimeSpan start, TimeSpan duration, TimelineItem? ignore = null)
    {
        if (start < TimeSpan.Zero || duration <= TimeSpan.Zero) return false;
        var end = start + duration;
        return timeline.Items.All(other => ReferenceEquals(other, ignore) || end <= other.Start || other.End <= start);
    }

    /// <summary>
    /// Puts an item on a timeline of its own kind. The kind of the timeline decides what a column and a row
    /// are for the device that plays it, so the .bin cannot mix kinds: anything else is refused.
    /// </summary>
    public static Edit AddItem(Timeline timeline, TimelineItem item)
    {
        if (!Accepts(timeline, item))
            throw new InvalidOperationException($"A '{timeline.Kind}' timeline does not take '{item.Kind}' items.");
        RequireHeight(timeline, item);
        // A pause is an entry too, and the file counts entries in 16 bits.
        if (timeline.Items.Count + 1 > MaxItemsPerTimeline)
            throw new InvalidOperationException($"A timeline holds at most {MaxItemsPerTimeline} items.");
        RequireWholeMs(item.Start, "Start");
        RequireWholeMs(item.Duration, "Duration");
        if (!Fits(timeline, item.Start, item.Duration))
            throw new InvalidOperationException("The item overlaps another item or lies outside the timeline.");
        return new InsertEdit<TimelineItem>("AddItem", timeline.Items, SortedIndex(timeline, item.Start, item), item);
    }

    public static Edit RemoveItem(Timeline timeline, TimelineItem item) =>
        new RemoveEdit<TimelineItem>("RemoveItem", timeline.Items, item);

    /// <summary>Moves an item in time (and keeps the list sorted). One edit per finished drag, not per mouse move.</summary>
    public static Edit MoveItem(Timeline timeline, TimelineItem item, TimeSpan newStart)
    {
        int from = RequireIndex(timeline, item);
        RequireWholeMs(newStart, nameof(newStart));
        if (!Fits(timeline, newStart, item.Duration, item))
            throw new InvalidOperationException("The item would overlap another item or leave the timeline.");

        var edits = new List<Edit>
        {
            new SetValueEdit<TimeSpan>("MoveItem", item, nameof(item.Start), () => item.Start, v => item.Start = v, newStart, mergeable: false),
        };
        int to = SortedIndexWithout(timeline, newStart, item);
        if (to != from) edits.Add(new MoveEdit<TimelineItem>("MoveItem", timeline.Items, from, to));
        return new GroupEdit("MoveItem", edits);
    }

    /// <summary>Changes the length of an item; its start stays.</summary>
    public static Edit ResizeItem(Timeline timeline, TimelineItem item, TimeSpan newDuration)
    {
        RequireIndex(timeline, item);
        RequireWholeMs(newDuration, nameof(newDuration));
        if (!Fits(timeline, item.Start, newDuration, item))
            throw new InvalidOperationException("The item would overlap the next item.");
        return new SetValueEdit<TimeSpan>("ResizeItem", item, nameof(item.Duration), () => item.Duration, v => item.Duration = v, newDuration, mergeable: false);
    }

    /// <summary>Moves the start of an item and keeps its end where it is (dragging the left edge).</summary>
    public static Edit TrimItemStart(Timeline timeline, TimelineItem item, TimeSpan newStart)
    {
        RequireIndex(timeline, item);
        RequireWholeMs(newStart, nameof(newStart));
        var newDuration = item.End - newStart;
        if (!Fits(timeline, newStart, newDuration, item))
            throw new InvalidOperationException("The item would overlap the previous item or have no length.");
        return new GroupEdit("ResizeItem",
        [
            new SetValueEdit<TimeSpan>("ResizeItem", item, nameof(item.Start), () => item.Start, v => item.Start = v, newStart, mergeable: false),
            new SetValueEdit<TimeSpan>("ResizeItem", item, nameof(item.Duration), () => item.Duration, v => item.Duration = v, newDuration, mergeable: false),
        ]);
    }

    /// <param name="percent">0 to 100.</param>
    public static Edit SetBrightness(TimelineItem item, byte percent)
    {
        if (percent > 100) throw new ArgumentOutOfRangeException(nameof(percent));
        RequireKind(item, "brightness", ItemKind.Image, ItemKind.Video, ItemKind.Gradient);
        return new SetValueEdit<byte>("SetBrightness", item, nameof(item.Brightness), () => item.Brightness, v => item.Brightness = v, percent, mergeable: true);
    }

    public static Edit SetStretching(TimelineItem item, int stretching)
    {
        RequireKind(item, "stretching", ItemKind.Image, ItemKind.Video, ItemKind.Gradient);
        // E-Pic writes the stretching times 5 in 16 bits.
        int max = item.Kind == ItemKind.Gradient ? MaxEpicStretching : ushort.MaxValue;
        if (stretching < MinStretching || stretching > max) throw new ArgumentOutOfRangeException(nameof(stretching));
        return new SetValueEdit<int>("SetStretching", item, nameof(item.Stretching), () => item.Stretching, v => item.Stretching = v, stretching, mergeable: true);
    }

    public static Edit SetLaser(TimelineItem item, bool on)
    {
        RequireKind(item, "laser", ItemKind.Image, ItemKind.Video, ItemKind.Gradient);
        return new SetValueEdit<bool>("SetLaser", item, nameof(item.LaserOn), () => item.LaserOn, v => item.LaserOn = v, on, mergeable: false);
    }

    /// <param name="color">#RRGGBB or #AARRGGBB; solid items and the first colour of effects.</param>
    public static Edit SetItemColor(TimelineItem item, string color)
    {
        RequireKind(item, "colour", ItemKind.Solid, ItemKind.Effect);
        Imaging.ColorText.ParseRgb(color);   // throws on a bad colour
        return new SetValueEdit<string?>("SetItemColor", item, nameof(item.Color), () => item.Color, v => item.Color = v, color, mergeable: true);
    }

    /// <param name="color">#RRGGBB or #AARRGGBB; the colour an effect goes to.</param>
    public static Edit SetItemColorLast(TimelineItem item, string color)
    {
        RequireKind(item, "second colour", ItemKind.Effect);
        Imaging.ColorText.ParseRgb(color);
        return new SetValueEdit<string?>("SetItemColor", item, nameof(item.ColorLast), () => item.ColorLast, v => item.ColorLast = v, color, mergeable: true);
    }

    /// <param name="effect">One of <see cref="LightEffectKind"/>.</param>
    public static Edit SetEffect(TimelineItem item, string effect)
    {
        RequireKind(item, "effect", ItemKind.Effect);
        if (effect is not (LightEffectKind.LinearTransition or LightEffectKind.SineWave or LightEffectKind.SquareWave or LightEffectKind.SawToothWave))
            throw new ArgumentOutOfRangeException(nameof(effect));
        return new SetValueEdit<string?>("SetEffect", item, nameof(item.Effect), () => item.Effect, v => item.Effect = v, effect, mergeable: false);
    }

    /// <summary>The time parameters of an effect, in milliseconds.</summary>
    public static Edit SetEffectTimes(TimelineItem item, int t1, int t2)
    {
        RequireKind(item, "effect times", ItemKind.Effect);
        if (t1 < 0 || t2 < 0) throw new ArgumentOutOfRangeException(nameof(t1));
        return new GroupEdit("SetEffectTimes",
        [
            new SetValueEdit<int>("SetEffectTimes", item, nameof(item.T1), () => item.T1, v => item.T1 = v, t1, mergeable: true),
            new SetValueEdit<int>("SetEffectTimes", item, nameof(item.T2), () => item.T2, v => item.T2 = v, t2, mergeable: true),
        ]);
    }

    /// <summary>Where the LED layout sits on a video frame.</summary>
    public static Edit SetOffset(TimelineItem item, int x, int y)
    {
        RequireKind(item, "offset", ItemKind.Video);
        return new GroupEdit("SetOffset",
        [
            new SetValueEdit<int>("SetOffset", item, nameof(item.OffsetX), () => item.OffsetX, v => item.OffsetX = v, x, mergeable: true),
            new SetValueEdit<int>("SetOffset", item, nameof(item.OffsetY), () => item.OffsetY, v => item.OffsetY = v, y, mergeable: true),
        ]);
    }

    /// <summary>
    /// Whether an item can be played by this timeline. Timelines only take their own kind of item,
    /// because each kind of device compiles its items differently.
    /// </summary>
    public static bool Accepts(Timeline timeline, TimelineItem item) => timeline.Kind switch
    {
        TimelineKind.Pixel => item.Kind == ItemKind.Image,
        TimelineKind.Video => item.Kind == ItemKind.Video,
        TimelineKind.EpicPoi => item.Kind == ItemKind.Gradient,
        TimelineKind.Light => item.Kind is ItemKind.Solid or ItemKind.Effect,
        _ => false,
    };

    /// <summary>A property that only some kinds of item have: the .bin has no field for it on the others.</summary>
    private static void RequireKind(TimelineItem item, string what, params string[] kinds)
    {
        if (!kinds.Contains(item.Kind))
            throw new InvalidOperationException($"A '{item.Kind}' item has no {what}.");
    }

    /// <summary>
    /// The height in LEDs that the pictures of a pixel timeline must have: the timeline's own, else the one of its
    /// first picture of known size. 0 when nothing fixes it yet (and for timelines of other kinds).
    /// </summary>
    public static int PixelHeight(Timeline timeline)
    {
        if (timeline.Kind != TimelineKind.Pixel) return 0;
        return timeline.Height > 0 ? timeline.Height : timeline.Items.FirstOrDefault(i => i.MediaHeight > 0)?.MediaHeight ?? 0;
    }

    /// <summary>Whether a picture of this height can go on the timeline (always, when either height is unknown).</summary>
    public static bool FitsHeight(Timeline timeline, int mediaHeight)
    {
        int height = PixelHeight(timeline);
        return height == 0 || mediaHeight == 0 || height == mediaHeight;
    }

    private static void RequireHeight(Timeline timeline, TimelineItem item)
    {
        if (!FitsHeight(timeline, item.MediaHeight))
            throw new InvalidOperationException(
                $"The picture is {item.MediaHeight} LEDs high, the timeline plays {PixelHeight(timeline)}.");
    }

    /// <summary>Fixes the number of LEDs of a pixel timeline. Pictures of another height must be taken off first.</summary>
    public static Edit SetTimelineHeight(Timeline timeline, int height)
    {
        if (timeline.Kind != TimelineKind.Pixel) throw new InvalidOperationException("Only a pixel timeline has a height.");
        if (height < 0 || height > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(height));
        if (height > 0 && timeline.Items.Any(i => i.MediaHeight > 0 && i.MediaHeight != height))
            throw new InvalidOperationException("The timeline holds pictures of another height.");
        return new SetValueEdit<int>("SetTimelineHeight", timeline, nameof(timeline.Height), () => timeline.Height, v => timeline.Height = v, height, mergeable: false);
    }

    private static bool IsKnownKind(string kind) =>
        kind is TimelineKind.Pixel or TimelineKind.Video or TimelineKind.EpicPoi or TimelineKind.Light;

    /// <summary>Takes an item off one timeline and puts it on another, at a new start. One undo step.</summary>
    public static Edit MoveItemToTimeline(Timeline from, Timeline to, TimelineItem item, TimeSpan newStart)
    {
        if (ReferenceEquals(from, to)) return MoveItem(from, item, newStart);
        RequireIndex(from, item);
        if (!Accepts(to, item)) throw new InvalidOperationException("The timeline does not take this kind of item.");
        RequireHeight(to, item);
        RequireWholeMs(newStart, nameof(newStart));
        if (!Fits(to, newStart, item.Duration)) throw new InvalidOperationException("The item would overlap another item or leave the timeline.");
        return new GroupEdit("MoveItem",
        [
            new RemoveEdit<TimelineItem>("MoveItem", from.Items, item),
            new SetValueEdit<TimeSpan>("MoveItem", item, nameof(item.Start), () => item.Start, v => item.Start = v, newStart, mergeable: false),
            new InsertEdit<TimelineItem>("MoveItem", to.Items, SortedIndex(to, newStart, null), item),
        ]);
    }

    // ---- library

    /// <summary>Puts an entry into the library. A media entry has a size (a missing file has none yet); a colour or effect has valid values.</summary>
    public static Edit AddLibraryEntry(ShowProject project, LibraryEntry entry)
    {
        CheckEntry(entry);
        if (project.Library.Any(e => e.Id == entry.Id)) throw new ArgumentException("The entry is already in the library.", nameof(entry));
        return new InsertEdit<LibraryEntry>("AddLibraryEntry", project.Library, project.Library.Count, entry);
    }

    /// <summary>Takes an entry out of the library. What an item plays cannot be taken out: the item would lose its picture.</summary>
    public static Edit RemoveLibraryEntry(ShowProject project, LibraryEntry entry)
    {
        if (Library.IsUsed(project, entry)) throw new InvalidOperationException("An item plays this entry.");
        return new RemoveEdit<LibraryEntry>("RemoveLibraryEntry", project.Library, entry);
    }

    public static Edit RenameLibraryEntry(LibraryEntry entry, string name) =>
        new SetValueEdit<string>("RenameLibraryEntry", entry, nameof(entry.Name), () => entry.Name, v => entry.Name = v, name, mergeable: true);

    /// <summary>
    /// Another file for an entry (a missing file found, a picture redone), and for every item that plays the old one.
    /// The new picture must be as high as the others on each pixel timeline it lands on.
    /// </summary>
    public static Edit ReplaceLibraryMedia(ShowProject project, LibraryEntry entry, string resourceId, string sourcePath, int width, int height, int frames = 0)
    {
        if (!Library.HasMedia(entry.Kind)) throw new InvalidOperationException("Only a picture, animation or gradient has a file.");
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));

        static bool Plays(LibraryEntry e, TimelineItem i) =>
            e.ResourceId is { } id ? i.ResourceId == id : i.ResourceId is null && !string.IsNullOrEmpty(e.SourcePath)
                && string.Equals(i.SourcePath, e.SourcePath, StringComparison.OrdinalIgnoreCase);

        var affected = project.Timelines.SelectMany(t => t.Items.Select(i => (Timeline: t, Item: i))).Where(x => Plays(entry, x.Item)).ToList();
        foreach (var timeline in affected.Select(x => x.Timeline).Distinct().Where(t => t.Kind == TimelineKind.Pixel))
        {
            var heights = timeline.Items.Select(i => Plays(entry, i) ? height : i.MediaHeight).Where(h => h > 0).Distinct().ToList();
            if (heights.Count > 1 || (timeline.Height > 0 && heights.Any(h => h != timeline.Height)))
                throw new InvalidOperationException($"The picture is {height} LEDs high, the timeline plays {PixelHeight(timeline)}.");
        }

        var edits = new List<Edit>();
        foreach (var (_, item) in affected)
        {
            edits.Add(new SetValueEdit<string?>("ReplaceLibraryMedia", item, nameof(item.ResourceId), () => item.ResourceId, v => item.ResourceId = v, resourceId, mergeable: false));
            edits.Add(new SetValueEdit<string?>("ReplaceLibraryMedia", item, nameof(item.SourcePath), () => item.SourcePath, v => item.SourcePath = v, sourcePath, mergeable: false));
            edits.Add(new SetValueEdit<int>("ReplaceLibraryMedia", item, nameof(item.MediaWidth), () => item.MediaWidth, v => item.MediaWidth = v, width, mergeable: false));
            edits.Add(new SetValueEdit<int>("ReplaceLibraryMedia", item, nameof(item.MediaHeight), () => item.MediaHeight, v => item.MediaHeight = v, height, mergeable: false));
        }
        edits.Add(new SetValueEdit<string?>("ReplaceLibraryMedia", entry, nameof(entry.ResourceId), () => entry.ResourceId, v => entry.ResourceId = v, resourceId, mergeable: false));
        edits.Add(new SetValueEdit<string?>("ReplaceLibraryMedia", entry, nameof(entry.SourcePath), () => entry.SourcePath, v => entry.SourcePath = v, sourcePath, mergeable: false));
        edits.Add(new SetValueEdit<int>("ReplaceLibraryMedia", entry, nameof(entry.Width), () => entry.Width, v => entry.Width = v, width, mergeable: false));
        edits.Add(new SetValueEdit<int>("ReplaceLibraryMedia", entry, nameof(entry.Height), () => entry.Height, v => entry.Height = v, height, mergeable: false));
        edits.Add(new SetValueEdit<int>("ReplaceLibraryMedia", entry, nameof(entry.Frames), () => entry.Frames, v => entry.Frames = v, frames, mergeable: false));
        return new GroupEdit("ReplaceLibraryMedia", edits);
    }

    private static void CheckEntry(LibraryEntry entry)
    {
        switch (entry.Kind)
        {
            case LibraryKind.Image or LibraryKind.Video or LibraryKind.Gradient:
                if (entry.ResourceId is not null && (entry.Width <= 0 || entry.Height <= 0))
                    throw new ArgumentException("A picture in the package has a size.", nameof(entry));
                break;
            case LibraryKind.Color:
                Imaging.ColorText.ParseRgb(entry.Color);
                break;
            case LibraryKind.Effect:
                Imaging.ColorText.ParseRgb(entry.Color);
                Imaging.ColorText.ParseRgb(entry.ColorLast);
                if (entry.Effect is not (LightEffectKind.LinearTransition or LightEffectKind.SineWave or LightEffectKind.SquareWave or LightEffectKind.SawToothWave))
                    throw new ArgumentException("Unknown light effect.", nameof(entry));
                if (entry.T1 < 0 || entry.T2 < 0) throw new ArgumentException("Effect times cannot be negative.", nameof(entry));
                break;
            default:
                throw new ArgumentException($"Unknown library kind '{entry.Kind}'.", nameof(entry));
        }
    }

    // ---- devices

    public static Edit AddDevice(ShowProject project, DeviceBinding device)
    {
        if (!IsKnownDeviceKind(device.DeviceType)) throw new ArgumentException($"Unknown device type '{device.DeviceType}'.", nameof(device));
        if (device.LedCount < 0 || device.LedCount > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(device));
        CheckTimelines(project, device, device.TimelineIds);
        return new InsertEdit<DeviceBinding>("AddDevice", project.Devices, project.Devices.Count, device);
    }

    public static Edit RemoveDevice(ShowProject project, DeviceBinding device) =>
        new RemoveEdit<DeviceBinding>("RemoveDevice", project.Devices, device);

    /// <summary>The timelines a device plays and their order. Each must be of the kind the device plays, and as high as the device.</summary>
    public static Edit SetDeviceTimelines(ShowProject project, DeviceBinding device, IReadOnlyList<Guid> timelineIds)
    {
        CheckTimelines(project, device, timelineIds);
        var old = device.TimelineIds.ToList();
        var next = timelineIds.ToList();
        return new SetValueEdit<List<Guid>>("SetDeviceTimelines", device, nameof(device.TimelineIds), () => old, v => device.TimelineIds = v.ToList(), next, mergeable: false);
    }

    /// <summary>The number of LEDs the device reported (or the user chose for a file export).</summary>
    public static Edit SetDeviceLedCount(ShowProject project, DeviceBinding device, int ledCount)
    {
        if (ledCount < 0 || ledCount > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(ledCount));
        CheckTimelines(project, device, device.TimelineIds, ledCount);
        return new SetValueEdit<int>("SetDeviceLedCount", device, nameof(device.LedCount), () => device.LedCount, v => device.LedCount = v, ledCount, mergeable: false);
    }

    /// <summary>The kind of timeline a kind of device plays.</summary>
    public static string TimelineKindOf(string deviceKind) => deviceKind switch
    {
        DeviceKind.Poi or DeviceKind.Hoop or DeviceKind.Fan => TimelineKind.Pixel,
        DeviceKind.Video => TimelineKind.Video,
        DeviceKind.EpicPoi => TimelineKind.EpicPoi,
        DeviceKind.Light => TimelineKind.Light,
        _ => throw new ArgumentException($"Unknown device type '{deviceKind}'.", nameof(deviceKind)),
    };

    /// <summary>The most timelines a device takes: a fan plays one, a video module one or two LED chains.</summary>
    public static int MaxTimelinesOf(string deviceKind) => deviceKind switch
    {
        DeviceKind.Fan => 1,
        DeviceKind.Video => 2,
        _ => byte.MaxValue,   // the .bin counts timelines (and LightMaster slots) in a byte
    };

    private static bool IsKnownDeviceKind(string kind) =>
        kind is DeviceKind.Poi or DeviceKind.Hoop or DeviceKind.Fan or DeviceKind.Video or DeviceKind.EpicPoi or DeviceKind.Light;

    private static void CheckTimelines(ShowProject project, DeviceBinding device, IReadOnlyList<Guid> ids, int? ledCount = null)
    {
        string kind = TimelineKindOf(device.DeviceType);
        if (ids.Count > MaxTimelinesOf(device.DeviceType))
            throw new InvalidOperationException($"A '{device.DeviceType}' plays at most {MaxTimelinesOf(device.DeviceType)} timelines.");
        if (ids.Distinct().Count() != ids.Count) throw new ArgumentException("A timeline is listed twice.", nameof(ids));
        foreach (var id in ids)
        {
            var timeline = project.Timelines.FirstOrDefault(t => t.Id == id)
                ?? throw new ArgumentException("The project has no such timeline.", nameof(ids));
            if (timeline.Kind != kind)
                throw new InvalidOperationException($"A '{device.DeviceType}' does not play '{timeline.Kind}' timelines.");
            int leds = ledCount ?? device.LedCount;
            // A poi or a hoop has as many LEDs as its pictures are high; a fan takes the height of its picture.
            if (device.DeviceType is DeviceKind.Poi or DeviceKind.Hoop && leds > 0 && !FitsHeight(timeline, leds))
                throw new InvalidOperationException($"The device has {leds} LEDs, the timeline plays {PixelHeight(timeline)}.");
        }
    }

    // ---- markers

    public static Edit AddMarker(ShowProject project, Marker marker)
    {
        if (marker.Position < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(marker));
        int at = project.Markers.FindIndex(m => m.Position > marker.Position);
        return new InsertEdit<Marker>("AddMarker", project.Markers, at < 0 ? project.Markers.Count : at, marker);
    }

    public static Edit RemoveMarker(ShowProject project, Marker marker) =>
        new RemoveEdit<Marker>("RemoveMarker", project.Markers, marker);

    public static Edit RenameMarker(Marker marker, string name) =>
        new SetValueEdit<string>("RenameMarker", marker, nameof(marker.Name), () => marker.Name, v => marker.Name = v, name, mergeable: true);

    public static Edit MoveMarker(ShowProject project, Marker marker, TimeSpan position)
    {
        int from = project.Markers.IndexOf(marker);
        if (from < 0) throw new InvalidOperationException("The marker is not in the project.");
        if (position < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(position));

        var edits = new List<Edit>
        {
            new SetValueEdit<TimeSpan>("MoveMarker", marker, nameof(marker.Position), () => marker.Position, v => marker.Position = v, position, mergeable: false),
        };
        var others = project.Markers.Where(m => !ReferenceEquals(m, marker)).ToList();
        int to = others.FindIndex(m => m.Position > position);
        if (to < 0) to = others.Count;
        if (to != from) edits.Add(new MoveEdit<Marker>("MoveMarker", project.Markers, from, to));
        return new GroupEdit("MoveMarker", edits);
    }

    // ---- helpers

    /// <summary>Several edits as one undo step.</summary>
    public static Edit Group(string key, params Edit[] edits) => new GroupEdit(key, edits);

    private static int RequireIndex(Timeline timeline, TimelineItem item)
    {
        int index = timeline.Items.IndexOf(item);
        return index >= 0 ? index : throw new InvalidOperationException("The item is not on the timeline.");
    }

    private static int SortedIndex(Timeline timeline, TimeSpan start, TimelineItem? ignore)
    {
        int at = timeline.Items.FindIndex(i => !ReferenceEquals(i, ignore) && i.Start > start);
        return at < 0 ? timeline.Items.Count : at;
    }

    /// <summary>Where the item sits once it is moved: its index in the list without it.</summary>
    private static int SortedIndexWithout(Timeline timeline, TimeSpan start, TimelineItem item)
    {
        var others = timeline.Items.Where(i => !ReferenceEquals(i, item)).ToList();
        int at = others.FindIndex(i => i.Start > start);
        return at < 0 ? others.Count : at;
    }
}
