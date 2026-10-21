using PixelTuner.Audio;
using PixelTuner.Core.Editing;
using PixelTuner.Core.Model;
using PixelTuner.Package;

namespace PixelTuner.UI.Timelines;

public enum HitKind
{
    None,
    /// <summary>The time ruler: a click moves the playhead.</summary>
    Ruler,
    /// <summary>The name column of a timeline.</summary>
    Header,
    /// <summary>Empty space on a timeline.</summary>
    Track,
    Item,
    ItemStart,
    ItemEnd,
    Marker,
    /// <summary>The soundtrack's waveform: a click moves the playhead, as on the ruler.</summary>
    Wave,
}

public readonly record struct Hit(HitKind Kind, Timeline? Timeline = null, TimelineItem? Item = null, Marker? Marker = null);

/// <summary>
/// What the timeline shows and does, without any drawing: where things are on screen, what a
/// click or drag hits, and the edits a drag ends in. The control only draws this and forwards
/// the pointer. Coordinates are those of the control: x from the left edge (the name column
/// first, then time), y from the top (the ruler first, then one row per timeline).
/// </summary>
public sealed class TimelineEditor
{
    public const double HeaderWidth = 170;
    public const double RulerHeight = 34;
    /// <summary>The soundtrack lane under the ruler; always there, so loading a sound does not shift the rows.</summary>
    public const double WaveHeight = 84;
    public const double TrackHeight = 64;
    public const double MinZoom = 4;
    public const double MaxZoom = 2000;

    private const double EdgeGrab = 6;
    private const double SnapDistance = 8;
    private static readonly TimeSpan MinItemLength = TimeSpan.FromMilliseconds(20);

    private readonly ProjectDocument _document;
    private DragState? _drag;

    public TimelineEditor(ProjectDocument document)
    {
        _document = document;
        document.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public ShowProject Project => _document.Project;

    /// <summary>Raised when something that is drawn changed: the project, view, selection, playhead or a drag.</summary>
    public event EventHandler? Changed;

    // ---- view

    public double PixelsPerSecond { get; private set; } = 100;

    /// <summary>The time at the left edge of the time area.</summary>
    public TimeSpan Scroll { get; private set; }

    /// <summary>Width of the whole control, set by the control when it is laid out.</summary>
    public double ViewWidth { get; set; } = 1000;

    public TimeSpan Playhead { get; private set; }

    /// <summary>Length of the soundtrack, once known: the timelines are drawn at least this far.</summary>
    public TimeSpan AudioDuration { get; set; }

    public double TimeToX(TimeSpan time) => HeaderWidth + (time - Scroll).TotalSeconds * PixelsPerSecond;

    public TimeSpan XToTime(double x) => Scroll + TimeSpan.FromSeconds((x - HeaderWidth) / PixelsPerSecond);

    public double TimeAreaWidth => Math.Max(1, ViewWidth - HeaderWidth);

    public TimeSpan VisibleDuration => TimeSpan.FromSeconds(TimeAreaWidth / PixelsPerSecond);

    /// <summary>The end of everything there is, so the view can scroll a little past it.</summary>
    public TimeSpan ContentEnd
    {
        get
        {
            var end = AudioDuration;
            foreach (var timeline in Project.Timelines)
                foreach (var item in timeline.Items)
                    if (item.End > end) end = item.End;
            foreach (var marker in Project.Markers)
                if (marker.Position > end) end = marker.Position;
            return end + TimeSpan.FromSeconds(10);
        }
    }

    /// <summary>Where the first timeline starts.</summary>
    public const double TracksTop = RulerHeight + WaveHeight;

    public double TotalHeight => TracksTop + Project.Timelines.Count * TrackHeight;

    public double TrackTop(int index) => TracksTop + index * TrackHeight;

    /// <summary>The waveform drawn in the sound lane; null until a soundtrack is read.</summary>
    public Waveform? Wave { get; set; }

    /// <summary>The soundtrack's player, if the platform has one. The playhead follows it while it plays.</summary>
    public IAudioPlayer? Player { get; set; }

    /// <summary>For the owner that changed <see cref="Wave"/> or <see cref="AudioDuration"/>: redraw.</summary>
    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public bool IsPlaying => Player?.State == AudioPlayerState.Playing;

    /// <summary>Puts the playhead somewhere (and the sound with it, when there is one).</summary>
    public void SetPlayhead(TimeSpan time)
    {
        Playhead = Clamp(time, TimeSpan.Zero, AudioDuration > TimeSpan.Zero ? AudioDuration : TimeSpan.MaxValue);
        Player?.Seek(Playhead);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void PlayPause()
    {
        if (Player is not { } player) return;
        if (player.State == AudioPlayerState.Playing)
        {
            player.Pause();
            return;
        }
        // At the end a second Play starts over; otherwise play from where the playhead stands.
        if (AudioDuration > TimeSpan.Zero && Playhead >= AudioDuration) Playhead = TimeSpan.Zero;
        player.Seek(Playhead);
        player.Play();
    }

    /// <summary>Stops the sound and goes back to the start, as PixelTuner 2 did.</summary>
    public void Stop()
    {
        _following = false;
        Player?.Stop();
        Playhead = TimeSpan.Zero;
        ScrollTo(TimeSpan.Zero);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Called regularly by the view while the sound plays: the playhead takes the player's position and the
    /// view pages along when the playhead leaves it. A track that ended leaves the playhead at its end.
    /// </summary>
    public void Tick()
    {
        if (Player is not { } player) return;
        var state = player.State;
        if (state == AudioPlayerState.Playing)
        {
            _following = true;
            Playhead = player.Position;
            FollowPlayhead();
        }
        else if (_following)
        {
            // It stopped on its own: paused (by the user or the system), or played to the end.
            _following = false;
            Playhead = state == AudioPlayerState.Paused ? player.Position : AudioDuration;
        }
        // The view calls this many times a second; only a real change is worth a redraw. Compared with what the
        // last call saw, not with the start of this one: the sound can change state between two calls.
        var now = (Playhead, Scroll, state);
        if (now == _lastTick) return;
        _lastTick = now;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private bool _following;
    private (TimeSpan Playhead, TimeSpan Scroll, AudioPlayerState State) _lastTick;

    private void FollowPlayhead()
    {
        var right = Scroll + VisibleDuration;
        if (Playhead < Scroll || Playhead > right) ScrollTo(Playhead - VisibleDuration * 0.1);
    }

    public void ScrollBy(double pixels) => ScrollTo(Scroll + TimeSpan.FromSeconds(pixels / PixelsPerSecond));

    public void ScrollTo(TimeSpan time)
    {
        var max = ContentEnd - VisibleDuration;
        if (max < TimeSpan.Zero) max = TimeSpan.Zero;
        var clamped = time < TimeSpan.Zero ? TimeSpan.Zero : time > max ? max : time;
        if (clamped == Scroll) return;
        Scroll = clamped;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Zooms by a factor and keeps the time under <paramref name="anchorX"/> where it is.</summary>
    public void ZoomAt(double factor, double anchorX)
    {
        double anchor = Math.Clamp(anchorX, HeaderWidth, Math.Max(HeaderWidth, ViewWidth));
        var timeAtAnchor = XToTime(anchor);
        PixelsPerSecond = Math.Clamp(PixelsPerSecond * factor, MinZoom, MaxZoom);
        Scroll = timeAtAnchor - TimeSpan.FromSeconds((anchor - HeaderWidth) / PixelsPerSecond);
        ScrollTo(Scroll);   // clamp
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ZoomToFit()
    {
        var end = ContentEnd - TimeSpan.FromSeconds(10);
        if (end < TimeSpan.FromSeconds(1)) end = TimeSpan.FromSeconds(10);
        PixelsPerSecond = Math.Clamp(TimeAreaWidth / end.TotalSeconds * 0.96, MinZoom, MaxZoom);
        Scroll = TimeSpan.Zero;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ---- selection

    /// <summary>The selected item, marker or timeline; null when nothing is.</summary>
    public object? Selected { get; private set; }

    public void Select(object? what)
    {
        if (ReferenceEquals(Selected, what)) return;
        Selected = what;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? SelectionChanged;

    /// <summary>The timeline an item or the selected timeline belongs to.</summary>
    public Timeline? TimelineOf(object? what) => what switch
    {
        Timeline t => t,
        TimelineItem i => Project.Timelines.FirstOrDefault(t => t.Items.Contains(i)),
        _ => null,
    };

    // ---- hit testing

    /// <summary>The timeline whose track lies at this height; null above or below the tracks.</summary>
    public Timeline? TimelineAt(double y)
    {
        if (y < TracksTop) return null;
        int row = (int)((y - TracksTop) / TrackHeight);
        return row < Project.Timelines.Count ? Project.Timelines[row] : null;
    }

    public Hit HitTest(double x, double y)
    {
        if (y < 0 || x < 0) return default;

        if (y < RulerHeight)
        {
            if (x >= HeaderWidth)
                foreach (var marker in Project.Markers)
                    if (Math.Abs(TimeToX(marker.Position) - x) <= EdgeGrab) return new Hit(HitKind.Marker, Marker: marker);
            return new Hit(HitKind.Ruler);
        }

        if (y < TracksTop) return x >= HeaderWidth ? new Hit(HitKind.Wave) : default;

        int row = (int)((y - TracksTop) / TrackHeight);
        if (row < 0 || row >= Project.Timelines.Count) return default;
        var timeline = Project.Timelines[row];
        if (x < HeaderWidth) return new Hit(HitKind.Header, timeline);

        // The item whose edge is grabbed wins over the one it touches.
        foreach (var item in timeline.Items)
        {
            double left = TimeToX(item.Start), right = TimeToX(item.End);
            if (x < left - EdgeGrab || x > right + EdgeGrab) continue;
            double grab = Math.Min(EdgeGrab, (right - left) / 3);
            if (Math.Abs(x - right) <= grab) return new Hit(HitKind.ItemEnd, timeline, item);
            if (Math.Abs(x - left) <= grab) return new Hit(HitKind.ItemStart, timeline, item);
            if (x >= left && x <= right) return new Hit(HitKind.Item, timeline, item);
        }
        return new Hit(HitKind.Track, timeline);
    }

    // ---- drag

    private sealed record DragState(Hit Hit, double StartX, TimeSpan OriginalStart, TimeSpan OriginalDuration, TimeSpan OriginalPosition);

    /// <summary>What is drawn while an item or marker is dragged; null when nothing is.</summary>
    public DragPreview? Preview { get; private set; }

    public bool IsDragging => _drag is not null;

    /// <summary>Pointer pressed: selects what was hit and starts a drag when it can move.</summary>
    public void PointerDown(double x, double y)
    {
        _drag = null;
        Preview = null;
        var hit = HitTest(x, y);
        switch (hit.Kind)
        {
            case HitKind.Item or HitKind.ItemStart or HitKind.ItemEnd:
                Select(hit.Item);
                _drag = new DragState(hit, x, hit.Item!.Start, hit.Item.Duration, default);
                break;
            case HitKind.Marker:
                Select(hit.Marker);
                _drag = new DragState(hit, x, default, default, hit.Marker!.Position);
                break;
            case HitKind.Header or HitKind.Track:
                Select(hit.Timeline);
                break;
            case HitKind.Ruler or HitKind.Wave:
                Select(null);
                SetPlayhead(XToTime(x));
                _drag = new DragState(hit, x, default, default, default);
                break;
            default:
                Select(null);
                break;
        }
    }

    /// <param name="y">Where the pointer is vertically; an item dragged to another timeline of its kind moves there. Null keeps it on its own.</param>
    public void PointerMove(double x, double? y = null)
    {
        if (_drag is not { } drag) return;
        switch (drag.Hit.Kind)
        {
            case HitKind.Ruler or HitKind.Wave:
                SetPlayhead(XToTime(Math.Max(x, HeaderWidth)));
                break;
            case HitKind.Marker:
                Preview = DragPreview.ForMarker(drag.Hit.Marker!, Max0(drag.OriginalPosition + Delta(drag, x)));
                Changed?.Invoke(this, EventArgs.Empty);
                break;
            default:
                Preview = ComputeItemPreview(drag, x, y);
                Changed?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    /// <summary>Pointer released: the drag becomes one undoable edit, if anything moved.</summary>
    public void PointerUp(double x, double? y = null)
    {
        if (_drag is not { } drag) return;
        PointerMove(x, y);
        var preview = Preview;
        _drag = null;
        Preview = null;

        if (preview is { } p)
        {
            var edit = p.ToEdit(Project, drag.Hit);
            if (edit is not null) _document.Execute(edit);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void CancelDrag()
    {
        _drag = null;
        Preview = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private TimeSpan Delta(DragState drag, double x) => TimeSpan.FromSeconds((x - drag.StartX) / PixelsPerSecond);

    private static TimeSpan Max0(TimeSpan t) => t < TimeSpan.Zero ? TimeSpan.Zero : t;

    private DragPreview ComputeItemPreview(DragState drag, double x, double? y)
    {
        var item = drag.Hit.Item!;
        var timeline = drag.Hit.Timeline!;
        var delta = Delta(drag, x);

        // Neighbours limit the move: items never overlap and never pass each other.
        var others = timeline.Items.Where(i => !ReferenceEquals(i, item)).ToList();
        var originalEnd = drag.OriginalStart + drag.OriginalDuration;
        var floor = others.Where(i => i.End <= drag.OriginalStart).Select(i => i.End).DefaultIfEmpty(TimeSpan.Zero).Max();
        var ceiling = others.Where(i => i.Start >= originalEnd).Select(i => i.Start).DefaultIfEmpty(TimeSpan.MaxValue).Min();
        var snapPoints = SnapPoints(item);

        TimeSpan start = drag.OriginalStart, duration = drag.OriginalDuration;
        switch (drag.Hit.Kind)
        {
            case HitKind.ItemStart:
            {
                start = Snap(drag.OriginalStart + delta, snapPoints);
                start = Clamp(start, floor, originalEnd - MinItemLength);
                duration = originalEnd - start;
                break;
            }
            case HitKind.ItemEnd:
            {
                var end = Snap(originalEnd + delta, snapPoints);
                end = Clamp(end, drag.OriginalStart + MinItemLength, ceiling);
                duration = end - start;
                break;
            }
            default:
            {
                var wanted = drag.OriginalStart + delta;
                // Snap whichever edge sticks; when both do, the closer one wins.
                var (byStart, startSnapped) = TrySnap(wanted, snapPoints);
                var (endSnap, endSnapped) = TrySnap(wanted + duration, snapPoints);
                var byEnd = endSnap - duration;
                start = (startSnapped, endSnapped) switch
                {
                    (true, false) => byStart,
                    (false, true) => byEnd,
                    (true, true) => Math.Abs((byStart - wanted).Ticks) <= Math.Abs((byEnd - wanted).Ticks) ? byStart : byEnd,
                    _ => wanted,
                };
                var latest = ceiling == TimeSpan.MaxValue ? TimeSpan.MaxValue : ceiling - duration;
                start = Clamp(start, floor, latest);

                // Over another timeline that takes this item and has room at the wanted time, it goes there.
                if (y is { } py && TimelineAt(py) is { } target && !ReferenceEquals(target, timeline) && Edits.Accepts(target, item) && Edits.FitsHeight(target, item.MediaHeight))
                {
                    var there = Max0(Snap(wanted, snapPoints));
                    there = Edits.WholeMs(there);
                    if (Edits.Fits(target, there, duration)) return DragPreview.ForItem(item, target, there, duration);
                }
                break;
            }
        }
        // The .bin counts whole milliseconds. Both edges are rounded, so neither crosses a neighbour's whole edge.
        var wholeStart = Edits.WholeMs(start);
        return DragPreview.ForItem(item, timeline, wholeStart, Edits.WholeMs(start + duration) - wholeStart);
    }

    private static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max) => value < min ? min : value > max ? max : value;

    /// <summary>Times an edge likes to stick to: zero, the playhead, markers and the edges of other items.</summary>
    private List<TimeSpan> SnapPoints(TimelineItem moving)
    {
        var points = new List<TimeSpan> { TimeSpan.Zero, Playhead };
        points.AddRange(Project.Markers.Select(m => m.Position));
        foreach (var timeline in Project.Timelines)
            foreach (var other in timeline.Items)
                if (!ReferenceEquals(other, moving))
                {
                    points.Add(other.Start);
                    points.Add(other.End);
                }
        return points;
    }

    private TimeSpan Snap(TimeSpan time, List<TimeSpan> points) => TrySnap(time, points).Time;

    private (TimeSpan Time, bool Snapped) TrySnap(TimeSpan time, List<TimeSpan> points)
    {
        var limit = TimeSpan.FromSeconds(SnapDistance / PixelsPerSecond);
        var best = time;
        var bestDistance = limit + TimeSpan.FromTicks(1);
        bool snapped = false;
        foreach (var point in points)
        {
            var distance = (point - time).Duration();
            if (distance < bestDistance)
            {
                best = point;
                bestDistance = distance;
                snapped = true;
            }
        }
        return (best, snapped);
    }

    // ---- commands on the selection

    public bool CanDelete => Selected is not null;

    public void DeleteSelected()
    {
        switch (Selected)
        {
            case TimelineItem item when TimelineOf(item) is { } timeline:
                _document.Execute(Edits.RemoveItem(timeline, item));
                break;
            case Marker marker:
                _document.Execute(Edits.RemoveMarker(Project, marker));
                break;
            case Timeline timeline:
                _document.Execute(Edits.RemoveTimeline(Project, timeline));
                break;
            default:
                return;
        }
        Select(null);
    }

    /// <summary>
    /// The first free stretch on a timeline at or after a time, at most <paramref name="wanted"/> long and
    /// at least <see cref="MinItemLength"/>; null when the timeline has none.
    /// </summary>
    public static (TimeSpan Start, TimeSpan Duration)? FindRoom(Timeline timeline, TimeSpan from, TimeSpan wanted)
    {
        var start = from;
        foreach (var item in timeline.Items)   // sorted by start, never overlapping
        {
            if (item.End <= start) continue;
            if (item.Start - start >= MinItemLength) return (start, Min(wanted, item.Start - start));
            start = item.End;
        }
        return (start, wanted);
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    /// <summary>
    /// Puts a new item on a timeline at the playhead, or after the item that is there, as long as <paramref name="wanted"/>
    /// or as the gap allows. The item's kind decides whether the timeline takes it. Null when there is no room.
    /// </summary>
    public TimelineItem? AddItem(Timeline timeline, TimelineItem item, TimeSpan wanted)
    {
        if (!Edits.Accepts(timeline, item) || !Edits.FitsHeight(timeline, item.MediaHeight) || FindRoom(timeline, Playhead, wanted) is not var (start, duration)) return null;
        item.Start = Edits.WholeMs(start);
        item.Duration = Edits.WholeMs(start + duration) - item.Start;
        _document.Execute(Edits.AddItem(timeline, item));
        Select(item);
        return item;
    }

    public Marker AddMarkerAtPlayhead(string name)
    {
        var marker = new Marker { Name = name, Position = Playhead };
        _document.Execute(Edits.AddMarker(Project, marker));
        Select(marker);
        return marker;
    }

    public Timeline AddTimeline(string name, string kind, string color)
    {
        var timeline = new Timeline { Name = name, Kind = kind, Color = color };
        _document.Execute(Edits.AddTimeline(Project, timeline));
        Select(timeline);
        return timeline;
    }
}

/// <summary>An item or marker as it would be if the drag ended now.</summary>
public sealed record DragPreview(TimelineItem? Item, Timeline? Timeline, Marker? Marker, TimeSpan Start, TimeSpan Duration)
{
    public static DragPreview ForItem(TimelineItem item, Timeline timeline, TimeSpan start, TimeSpan duration) =>
        new(item, timeline, null, start, duration);

    public static DragPreview ForMarker(Marker marker, TimeSpan position) => new(null, null, marker, position, TimeSpan.Zero);

    internal Edit? ToEdit(ShowProject project, Hit hit)
    {
        if (Marker is { } marker)
            return marker.Position == Start ? null : Edits.MoveMarker(project, marker, Start);

        var item = Item!;
        var timeline = Timeline!;
        if (!ReferenceEquals(hit.Timeline, timeline)) return Edits.MoveItemToTimeline(hit.Timeline!, timeline, item, Start);
        if (item.Start == Start && item.Duration == Duration) return null;
        return hit.Kind switch
        {
            HitKind.ItemStart => Edits.TrimItemStart(timeline, item, Start),
            HitKind.ItemEnd => Edits.ResizeItem(timeline, item, Duration),
            _ => Edits.MoveItem(timeline, item, Start),
        };
    }
}
