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

    // ---- timelines

    public static Edit AddTimeline(ShowProject project, Timeline timeline, int? index = null)
    {
        int at = index ?? project.Timelines.Count;
        if (at < 0 || at > project.Timelines.Count) throw new ArgumentOutOfRangeException(nameof(index));
        return new InsertEdit<Timeline>("AddTimeline", project.Timelines, at, timeline);
    }

    public static Edit RemoveTimeline(ShowProject project, Timeline timeline)
    {
        var edits = new List<Edit>();
        // Devices that played this timeline stay bound to nothing, and come back with undo.
        foreach (var binding in project.Devices.Where(d => d.TimelineId == timeline.Id))
            edits.Add(new SetValueEdit<Guid?>("RemoveTimeline", binding, nameof(binding.TimelineId),
                () => binding.TimelineId, v => binding.TimelineId = v, null, mergeable: false));
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

    // ---- items

    /// <summary>Whether an item of this length fits at this start without touching its neighbours' time.</summary>
    /// <param name="ignore">The item being moved or resized, which does not block itself.</param>
    public static bool Fits(Timeline timeline, TimeSpan start, TimeSpan duration, TimelineItem? ignore = null)
    {
        if (start < TimeSpan.Zero || duration <= TimeSpan.Zero) return false;
        var end = start + duration;
        return timeline.Items.All(other => ReferenceEquals(other, ignore) || end <= other.Start || other.End <= start);
    }

    public static Edit AddItem(Timeline timeline, TimelineItem item)
    {
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
        if (!Fits(timeline, item.Start, newDuration, item))
            throw new InvalidOperationException("The item would overlap the next item.");
        return new SetValueEdit<TimeSpan>("ResizeItem", item, nameof(item.Duration), () => item.Duration, v => item.Duration = v, newDuration, mergeable: false);
    }

    /// <summary>Moves the start of an item and keeps its end where it is (dragging the left edge).</summary>
    public static Edit TrimItemStart(Timeline timeline, TimelineItem item, TimeSpan newStart)
    {
        RequireIndex(timeline, item);
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
        return new SetValueEdit<byte>("SetBrightness", item, nameof(item.Brightness), () => item.Brightness, v => item.Brightness = v, percent, mergeable: true);
    }

    public static Edit SetStretching(TimelineItem item, int stretching)
    {
        if (stretching < 1) throw new ArgumentOutOfRangeException(nameof(stretching));
        return new SetValueEdit<int>("SetStretching", item, nameof(item.Stretching), () => item.Stretching, v => item.Stretching = v, stretching, mergeable: true);
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
