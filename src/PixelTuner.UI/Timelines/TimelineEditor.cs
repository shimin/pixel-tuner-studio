using PixelTuner.Audio;
using PixelTuner.Core.Editing;
using PixelTuner.Core.Model;
using PixelTuner.UI.Resources;
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

    /// <summary>Item edges are dragged in steps of this many milliseconds.</summary>
    public const int GridMilliseconds = 50;

    private static readonly TimeSpan Grid = TimeSpan.FromMilliseconds(GridMilliseconds);
    private static readonly TimeSpan MinItemLength = Grid;

    private readonly ProjectDocument _document;
    private DragState? _drag;

    public TimelineEditor(ProjectDocument document)
    {
        Thumbnails = new ItemThumbnails(id => document.OpenMedia(id));
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

    private readonly List<TimelineItem> _selectedItems = [];

    /// <summary>
    /// Every selected item, in the order they were picked; <see cref="Selected"/> is the last one picked (the one the
    /// inspector shows and the others can be given the settings of). Empty unless items are selected.
    /// </summary>
    public IReadOnlyList<TimelineItem> SelectedItems => _selectedItems;

    public bool IsSelected(TimelineItem item) => _selectedItems.Contains(item);

    public void Select(object? what)
    {
        if (ReferenceEquals(Selected, what) && _selectedItems.Count <= 1) return;
        Selected = what;
        _selectedItems.Clear();
        if (what is TimelineItem item) _selectedItems.Add(item);
        RaiseSelectionChanged();
    }

    /// <summary>Adds an item to the selection, or takes it out when it is in (Ctrl or Cmd and a click).</summary>
    public void ToggleItem(TimelineItem item)
    {
        if (Selected is not TimelineItem)
        {
            Select(item);
            return;
        }
        if (!_selectedItems.Remove(item)) _selectedItems.Add(item);
        Selected = _selectedItems.Count == 0 ? null : _selectedItems[^1];
        RaiseSelectionChanged();
    }

    /// <summary>
    /// Selects the items from the current one to this one on the same timeline (Shift and a click). On another timeline,
    /// or with no item selected, it selects just this one.
    /// </summary>
    public void SelectRangeTo(TimelineItem item)
    {
        var timeline = TimelineOf(item);
        if (Selected is not TimelineItem from || timeline is null || TimelineOf(from) != timeline)
        {
            Select(item);
            return;
        }
        int a = timeline.Items.IndexOf(from), b = timeline.Items.IndexOf(item);
        _selectedItems.Clear();
        for (int i = Math.Min(a, b); i <= Math.Max(a, b); i++) _selectedItems.Add(timeline.Items[i]);
        // The item picked last is the one the inspector shows.
        _selectedItems.Remove(item);
        _selectedItems.Add(item);
        Selected = item;
        RaiseSelectionChanged();
    }

    private void RaiseSelectionChanged()
    {
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

    // ---- an LED layout dragged here from the canvas

    /// <summary>The timeline an LED layout is being dragged over, and whether it would take it (only a video timeline plays from a layout).</summary>
    public LayoutDropHint? LayoutDrop { get; private set; }

    /// <summary>The pointer with a dragged layout is at this height: the timeline under it is marked as a target or as one that refuses.</summary>
    public void HoverLayout(double y)
    {
        var target = TimelineAt(y);
        var hint = target is null ? null : new LayoutDropHint(target, target.Kind == TimelineKind.Video);
        if (hint == LayoutDrop) return;
        LayoutDrop = hint;
        RaiseChanged();
    }

    /// <summary>The drag left the timelines or ended.</summary>
    public void LeaveLayout()
    {
        if (LayoutDrop is null) return;
        LayoutDrop = null;
        RaiseChanged();
    }

    /// <summary>
    /// The layout is dropped at this height: a video timeline takes it (one undo step) and is selected; any other does not. True when the
    /// layout went to a timeline.
    /// </summary>
    public bool DropLayout(double y, LedLayout layout)
    {
        LeaveLayout();
        if (TimelineAt(y) is not { Kind: TimelineKind.Video } target || !Project.LedLayouts.Contains(layout)) return false;
        if (target.LedLayoutId != layout.Id) _document.Execute(Edits.SetLedLayout(Project, target, layout.Id));
        Select(target);
        return true;
    }

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
    /// <param name="mode">Ctrl or Cmd adds or takes out an item, Shift selects a run of items; neither starts a drag.</param>
    public void PointerDown(double x, double y, SelectionMode mode = SelectionMode.Replace)
    {
        _drag = null;
        Preview = null;
        var hit = HitTest(x, y);
        if (hit.Item is { } clicked && mode != SelectionMode.Replace)
        {
            if (mode == SelectionMode.Toggle) ToggleItem(clicked);
            else SelectRangeTo(clicked);
            return;
        }
        switch (hit.Kind)
        {
            case HitKind.Item or HitKind.ItemStart or HitKind.ItemEnd:
                // A click on an item of a selection that is already several keeps them, so they can be seen while the item is dragged;
                // a drag moves just the item that was grabbed.
                if (!(IsSelected(hit.Item!) && _selectedItems.Count > 1)) Select(hit.Item);
                else Selected = hit.Item;
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
        DropDenial? denial = null;
        switch (drag.Hit.Kind)
        {
            case HitKind.ItemStart:
            {
                start = Snap(drag.OriginalStart + delta, snapPoints);
                start = OnGrid(Clamp(start, floor, originalEnd - MinItemLength), floor, originalEnd - MinItemLength);
                duration = originalEnd - start;
                break;
            }
            case HitKind.ItemEnd:
            {
                var end = Snap(originalEnd + delta, snapPoints);
                end = OnGrid(Clamp(end, drag.OriginalStart + MinItemLength, ceiling), drag.OriginalStart + MinItemLength, ceiling);
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
                start = OnGrid(Clamp(start, floor, latest), floor, latest);

                // Over another timeline that takes this item and has room at the wanted time, it goes there.
                // Over one that cannot take it, the item stays and the drag says why.
                if (y is { } py && TimelineAt(py) is { } target && !ReferenceEquals(target, timeline))
                {
                    if (!Edits.Accepts(target, item))
                        denial = new DropDenial(target, DropDenialReason.WrongKind, 0, 0);
                    else if (!Edits.FitsHeight(target, item.MediaHeight))
                        denial = new DropDenial(target, DropDenialReason.WrongHeight, Edits.PixelHeight(target), item.MediaHeight);
                    else
                    {
                        var there = Edits.WholeMs(OnGrid(Max0(Snap(wanted, snapPoints)), TimeSpan.Zero, TimeSpan.MaxValue));
                        if (Edits.Fits(target, there, duration)) return DragPreview.ForItem(item, target, there, duration);
                        denial = new DropDenial(target, DropDenialReason.NoRoom, 0, 0);
                    }
                }
                break;
            }
        }
        // The .bin counts whole milliseconds. Both edges are rounded, so neither crosses a neighbour's whole edge.
        var wholeStart = Edits.WholeMs(start);
        return DragPreview.ForItem(item, timeline, wholeStart, Edits.WholeMs(start + duration) - wholeStart) with { Denial = denial };
    }

    /// <summary>
    /// The nearest multiple of the grid step inside [<paramref name="min"/>, <paramref name="max"/>]. When no multiple
    /// fits (a neighbour's edge is off the grid, as in an imported project), the limit itself: items never overlap.
    /// </summary>
    internal static TimeSpan OnGrid(TimeSpan time, TimeSpan min, TimeSpan max)
    {
        long step = Grid.Ticks;
        long ticks = (long)Math.Round((double)time.Ticks / step, MidpointRounding.AwayFromZero) * step;
        if (ticks < min.Ticks)
        {
            long up = (min.Ticks + step - 1) / step * step;
            return up <= max.Ticks ? TimeSpan.FromTicks(up) : min;
        }
        if (ticks > max.Ticks)
        {
            long down = max.Ticks / step * step;
            return down >= min.Ticks ? TimeSpan.FromTicks(down) : max;
        }
        return TimeSpan.FromTicks(ticks);
    }

    private static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max) => value < min ? min : value > max ? max : value;

    /// <summary>Times an edge likes to stick to: zero, the playhead and markers (rounded to the grid) and the edges of other items.</summary>
    private List<TimeSpan> SnapPoints(TimelineItem moving)
    {
        // The playhead and the markers are anywhere in time, but an edge only lands on the grid: it sticks to the
        // nearest multiple of the step (1.890 s pulls to 1.900 s, 1.870 s to 1.850 s).
        var points = new List<TimeSpan> { TimeSpan.Zero, OnGrid(Playhead, TimeSpan.Zero, TimeSpan.MaxValue) };
        points.AddRange(Project.Markers.Select(m => OnGrid(m.Position, TimeSpan.Zero, TimeSpan.MaxValue)));
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

    // ---- what a lane shows of an item

    /// <summary>Thumbnails of the pictures on the lanes.</summary>
    public ItemThumbnails Thumbnails { get; }

    /// <summary>A pass narrower than this many pixels is not drawn as a picture but as its average colour.</summary>
    public const double MinTileWidth = 3;

    /// <summary>A pass at least this wide gets a line at its end, so the user can see where the device starts over.</summary>
    public const double PassLineWidth = 14;

    /// <summary>
    /// Where one picture is repeated across an item. The device repeats the item from its start to its end at the speed
    /// the stretching sets, so pass <c>n</c> begins at <c>left + n * PassWidth</c>; the last pass is cut off by the item's end.
    /// </summary>
    /// <param name="Averaged">The passes are too narrow to tell apart: draw the average colour.</param>
    public readonly record struct TileRun(double PassWidth, int FirstPass, int LastPass, bool Averaged);

    /// <param name="left">Where the item starts, in pixels.</param>
    /// <param name="right">Where the item ends.</param>
    /// <param name="pass">How long one pass takes.</param>
    /// <param name="clipLeft">Passes wholly outside [clipLeft, clipRight] are left out.</param>
    public TileRun TileLayout(double left, double right, TimeSpan pass, double clipLeft, double clipRight)
    {
        double width = pass.TotalSeconds * PixelsPerSecond;
        if (pass <= TimeSpan.Zero || width < MinTileWidth || right <= left) return new TileRun(width, 0, -1, Averaged: true);
        int first = (int)Math.Max(0, Math.Floor((clipLeft - left) / width));
        int last = (int)Math.Min(Math.Ceiling((right - left) / width) - 1, Math.Floor((clipRight - left) / width));
        return new TileRun(width, first, last, Averaged: false);
    }

    /// <summary>
    /// The pauses of a timeline: the stretch before the first item and the gaps between items. The device plays each as a black
    /// entry (the .bin has one for it), so the lane draws it black.
    /// </summary>
    public static IReadOnlyList<(TimeSpan Start, TimeSpan End)> Pauses(Timeline timeline)
    {
        var result = new List<(TimeSpan, TimeSpan)>();
        var items = timeline.Items;
        if (items.Count == 0) return result;
        if (items[0].Start > TimeSpan.Zero) result.Add((TimeSpan.Zero, items[0].Start));
        for (int i = 0; i + 1 < items.Count; i++)
            if (items[i].End < items[i + 1].Start) result.Add((items[i].End, items[i + 1].Start));
        return result;
    }

    // ---- commands on the selection

    public bool CanDelete => Selected is not null;

    public void DeleteSelected()
    {
        if (_selectedItems.Count > 1)
        {
            _document.Execute(Edits.RemoveItems(Project, _selectedItems.ToList()));
            Select(null);
            return;
        }
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
        // Start on the grid when the gap has room for it; the length stays as asked unless the gap cuts it.
        long step = Grid.Ticks;
        var gridStart = TimeSpan.FromTicks((start.Ticks + step - 1) / step * step);
        var end = start + duration;
        var gridEnd = duration < wanted ? TimeSpan.FromTicks(end.Ticks / step * step) : gridStart + wanted;
        if (gridEnd - gridStart >= MinItemLength && (duration < wanted || FindRoom(timeline, gridStart, wanted) is { } room && room.Start == gridStart && room.Duration == wanted))
        {
            start = gridStart;
            end = gridEnd;
        }
        item.Start = Edits.WholeMs(start);
        item.Duration = Edits.WholeMs(end) - item.Start;
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

/// <summary>What a click does to the selection.</summary>
public enum SelectionMode
{
    /// <summary>The clicked item becomes the selection.</summary>
    Replace,

    /// <summary>The clicked item is added to the selection, or taken out of it.</summary>
    Toggle,

    /// <summary>The run of items from the selected one to the clicked one is selected.</summary>
    Range,
}

public enum DropDenialReason
{
    /// <summary>The timeline plays another kind of item.</summary>
    WrongKind,

    /// <summary>A pixel timeline plays pictures of another height.</summary>
    WrongHeight,

    /// <summary>The item does not fit between the neighbours at that time.</summary>
    NoRoom,
}

/// <summary>Why an item dragged over a timeline will not go there.</summary>
/// <param name="RequiredHeight">WrongHeight: what the timeline plays.</param>
/// <param name="ItemHeight">WrongHeight: what the item is.</param>
public sealed record DropDenial(Timeline Target, DropDenialReason Reason, int RequiredHeight, int ItemHeight)
{
    /// <summary>The words shown over the timeline.</summary>
    public string Text() => Reason switch
    {
        DropDenialReason.WrongKind => string.Format(Strings.Drop_WrongKind, KindName(Target.Kind)),
        DropDenialReason.WrongHeight => string.Format(Strings.Drop_WrongHeight, RequiredHeight, ItemHeight),
        _ => Strings.Drop_NoRoom,
    };

    internal static string KindName(string kind) => kind switch
    {
        TimelineKind.Video => Strings.Kind_Video,
        TimelineKind.EpicPoi => Strings.Kind_EpicPoi,
        TimelineKind.Light => Strings.Kind_Light,
        _ => Strings.Kind_Pixel,
    };
}

/// <summary>A timeline under a layout that is being dragged, and whether it would take it.</summary>
public sealed record LayoutDropHint(Timeline Target, bool Allowed)
{
    /// <summary>The words shown over a timeline that refuses.</summary>
    public string Text() => Allowed ? "" : Strings.Drop_LayoutOnlyVideo;
}

/// <summary>An item or marker as it would be if the drag ended now.</summary>
public sealed record DragPreview(TimelineItem? Item, Timeline? Timeline, Marker? Marker, TimeSpan Start, TimeSpan Duration)
{
    /// <summary>Set while the pointer is over a timeline that will not take the item.</summary>
    public DropDenial? Denial { get; init; }

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
