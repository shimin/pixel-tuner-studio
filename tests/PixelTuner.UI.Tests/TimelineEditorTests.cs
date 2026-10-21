using PixelTuner.Core.Editing;
using PixelTuner.Core.Model;
using PixelTuner.Package;
using PixelTuner.UI.Timelines;

namespace PixelTuner.UI.Tests;

public class TimelineEditorTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    private readonly ProjectDocument _doc = ProjectDocument.CreateNew("Show");
    private readonly TimelineEditor _editor;

    public TimelineEditorTests() => _editor = new TimelineEditor(_doc) { ViewWidth = 1170 };   // 1000 px of time at 100 px/s

    private Timeline Track(params (double start, double length)[] items)
    {
        var timeline = new Timeline { Name = "T" + (_doc.Project.Timelines.Count + 1) };
        foreach (var (start, length) in items)
            timeline.Items.Add(new TimelineItem { Start = S(start), Duration = S(length) });
        _doc.Project.Timelines.Add(timeline);
        return timeline;
    }

    private static void AssertNear(double expectedSeconds, TimeSpan actual) =>
        Assert.InRange(actual.TotalSeconds, expectedSeconds - 0.02, expectedSeconds + 0.02);

    private double X(double seconds) => _editor.TimeToX(S(seconds));
    private static double RowY(int row) => TimelineEditor.TracksTop + row * TimelineEditor.TrackHeight + 10;

    [Fact]
    public void Time_and_x_are_inverse_and_follow_zoom_and_scroll()
    {
        Assert.Equal(TimelineEditor.HeaderWidth + 200, X(2), 6);
        Assert.Equal(S(2), _editor.XToTime(X(2)));

        _editor.ZoomAt(2, TimelineEditor.HeaderWidth);
        Assert.Equal(TimelineEditor.HeaderWidth + 400, X(2), 6);
    }

    [Fact]
    public void Zoom_keeps_the_time_under_the_pointer()
    {
        Track((0, 100));
        _editor.ScrollTo(S(10));
        double anchor = TimelineEditor.HeaderWidth + 300;
        var before = _editor.XToTime(anchor);

        _editor.ZoomAt(2.5, anchor);

        Assert.Equal(before.TotalSeconds, _editor.XToTime(anchor).TotalSeconds, 3);
        Assert.Equal(250, _editor.PixelsPerSecond, 6);
    }

    [Fact]
    public void Zoom_and_scroll_stay_within_limits()
    {
        Track((0, 1000));
        _editor.ZoomAt(1e6, TimelineEditor.HeaderWidth);
        Assert.Equal(TimelineEditor.MaxZoom, _editor.PixelsPerSecond);
        _editor.ZoomAt(1e-9, TimelineEditor.HeaderWidth);
        Assert.Equal(TimelineEditor.MinZoom, _editor.PixelsPerSecond);

        _editor.ScrollBy(-1e6);
        Assert.Equal(TimeSpan.Zero, _editor.Scroll);
        _editor.ScrollBy(1e9);
        AssertNear(_editor.ContentEnd.TotalSeconds, _editor.Scroll + _editor.VisibleDuration);   // scrolled to the very end, no further
    }

    [Fact]
    public void Fit_shows_the_whole_show()
    {
        Track((0, 60));
        _editor.ZoomToFit();
        Assert.True(X(60) < _editor.ViewWidth);
        Assert.True(X(60) > _editor.ViewWidth * 0.8);
    }

    [Fact]
    public void Hit_testing_finds_ruler_headers_items_edges_markers_and_empty_track()
    {
        var timeline = Track((1, 2), (5, 1));
        var marker = new Marker { Name = "m", Position = S(8) };
        _doc.Project.Markers.Add(marker);

        Assert.Equal(HitKind.Ruler, _editor.HitTest(X(3), 10).Kind);
        Assert.Equal(HitKind.Marker, _editor.HitTest(X(8) + 2, 10).Kind);
        Assert.Equal(HitKind.Header, _editor.HitTest(20, RowY(0)).Kind);
        Assert.Equal(HitKind.Track, _editor.HitTest(X(4), RowY(0)).Kind);
        Assert.Equal(HitKind.None, _editor.HitTest(X(4), RowY(3)).Kind);

        var middle = _editor.HitTest(X(2), RowY(0));
        Assert.Equal(HitKind.Item, middle.Kind);
        Assert.Same(timeline.Items[0], middle.Item);
        Assert.Equal(HitKind.ItemStart, _editor.HitTest(X(1) + 1, RowY(0)).Kind);
        Assert.Equal(HitKind.ItemEnd, _editor.HitTest(X(3) - 1, RowY(0)).Kind);
    }

    [Fact]
    public void Clicking_selects_and_the_ruler_moves_the_playhead()
    {
        var timeline = Track((1, 2));
        _editor.PointerDown(X(2), RowY(0));
        _editor.PointerUp(X(2));
        Assert.Same(timeline.Items[0], _editor.Selected);

        _editor.PointerDown(20, RowY(0));
        _editor.PointerUp(20);
        Assert.Same(timeline, _editor.Selected);

        _editor.PointerDown(X(7), 10);
        _editor.PointerMove(X(9));
        _editor.PointerUp(X(9));
        Assert.Equal(S(9), _editor.Playhead);
        Assert.Null(_editor.Selected);
        Assert.False(_doc.IsDirty);   // moving the playhead is not a change of the project
    }

    [Fact]
    public void Dragging_an_item_moves_it_in_one_undoable_edit()
    {
        var timeline = Track((1, 2));
        var item = timeline.Items[0];

        _editor.PointerDown(X(2), RowY(0));
        _editor.PointerMove(X(2) + 150);
        Assert.NotNull(_editor.Preview);
        Assert.Equal(S(1), item.Start);          // nothing changes until the pointer is released
        _editor.PointerMove(X(2) + 250.4);
        _editor.PointerUp(X(2) + 250.4);

        Assert.Equal(3.5, item.Start.TotalSeconds, 2);
        Assert.Equal(S(2), item.Duration);
        Assert.Equal("MoveItem", _doc.UndoKey);
        _doc.Undo();
        Assert.Equal(S(1), item.Start);
        Assert.False(_doc.CanUndo);
    }

    [Fact]
    public void A_click_without_a_drag_changes_nothing()
    {
        var timeline = Track((1, 2));
        _editor.PointerDown(X(2), RowY(0));
        _editor.PointerUp(X(2));
        Assert.False(_doc.CanUndo);
        Assert.Equal(S(1), timeline.Items[0].Start);
    }

    [Fact]
    public void Items_stop_at_their_neighbours_and_at_zero()
    {
        var timeline = Track((2, 1), (5, 1), (8, 1));
        var middle = timeline.Items[1];

        _editor.PointerDown(X(5.5), RowY(0));
        _editor.PointerUp(X(5.5) + 1000);        // far to the right: stops at the next item
        Assert.Equal(S(7), middle.Start);

        _editor.PointerDown(X(7.5), RowY(0));
        _editor.PointerUp(X(7.5) - 3000);        // far to the left: stops at the previous one
        Assert.Equal(S(3), middle.Start);

        var first = timeline.Items[0];
        _editor.PointerDown(X(2.5), RowY(0));
        _editor.PointerUp(X(2.5) - 3000);
        Assert.Equal(TimeSpan.Zero, first.Start);
    }

    [Fact]
    public void An_edge_sticks_to_the_edge_of_another_item_and_to_markers()
    {
        var timeline = Track((0, 2), (6, 1));
        var second = timeline.Items[1];
        _doc.Project.Markers.Add(new Marker { Name = "m", Position = S(4.5) });

        // Aim 3 pixels short of the first item's end: close enough to stick.
        _editor.PointerDown(X(6.5), RowY(0));
        _editor.PointerUp(X(6.5) - 4 * 100 + 3);
        Assert.Equal(S(2), second.Start);   // exactly the end of the first item, not 2.03

        // And to a marker: aim 4 pixels past it.
        _editor.PointerDown(X(2.5), RowY(0));
        _editor.PointerUp(X(2.5) + 2.5 * 100 - 4 + 0.0);
        Assert.Equal(S(4.5), second.Start);
    }

    [Fact]
    public void A_drag_ends_on_whole_milliseconds_whatever_the_zoom_and_the_pixel()
    {
        var timeline = Track((1, 2), (6, 1));
        var item = timeline.Items[0];
        _editor.ZoomAt(1.37, TimelineEditor.HeaderWidth);

        double x = X(2);
        _editor.PointerDown(x, RowY(0));
        _editor.PointerMove(x + 123.4567);
        _editor.PointerUp(x + 123.4567);
        Assert.True(Edits.IsWholeMs(item.Start) && Edits.IsWholeMs(item.Duration));
        Assert.NotEqual(S(1), item.Start);

        // The right edge too, against the neighbour at 6 s.
        x = X(item.End.TotalSeconds) - 1;
        _editor.PointerDown(x, RowY(0));
        _editor.PointerMove(x + 77.7777);
        _editor.PointerUp(x + 77.7777);
        Assert.True(Edits.IsWholeMs(item.Start) && Edits.IsWholeMs(item.Duration));
        Assert.True(item.End <= S(6));
    }

    private static bool OnGrid(TimeSpan t) => t.Ticks % TimeSpan.FromMilliseconds(TimelineEditor.GridMilliseconds).Ticks == 0;

    [Theory]
    [InlineData(0.0)]
    [InlineData(37.3)]
    [InlineData(81.9)]
    [InlineData(-52.4)]
    public void Moving_an_item_puts_its_start_on_a_multiple_of_50_ms(double dragPixels)
    {
        var timeline = Track((2, 2));   // 100 px per second: one pixel is 10 ms
        var item = timeline.Items[0];

        _editor.PointerDown(X(3), RowY(0));
        _editor.PointerMove(X(3) + dragPixels);
        _editor.PointerUp(X(3) + dragPixels);

        Assert.True(OnGrid(item.Start), item.Start.ToString());
        Assert.True(OnGrid(item.End), item.End.ToString());
        Assert.Equal(S(2), item.Duration);
    }

    [Fact]
    public void Dragging_either_edge_puts_that_edge_on_a_multiple_of_50_ms()
    {
        var timeline = Track((2, 2));
        var item = timeline.Items[0];

        _editor.PointerDown(X(2) + 1, RowY(0));    // left edge
        _editor.PointerMove(X(2) + 1 + 17.7);
        _editor.PointerUp(X(2) + 1 + 17.7);
        Assert.True(OnGrid(item.Start));
        Assert.Equal(S(4), item.End);

        _editor.PointerDown(X(4) - 1, RowY(0));    // right edge
        _editor.PointerMove(X(4) - 1 + 63.3);
        _editor.PointerUp(X(4) - 1 + 63.3);
        Assert.True(OnGrid(item.End));
        Assert.True(OnGrid(item.Duration));
    }

    [Fact]
    public void A_neighbour_off_the_grid_is_never_overlapped_by_the_rounding()
    {
        // An imported project can have edges anywhere.
        var timeline = Track((1, 0.973), (3, 1));   // the first ends at 1.973 s
        var item = timeline.Items[1];

        _editor.PointerDown(X(3) + 1, RowY(0));    // left edge of the second, dragged to the left neighbour
        _editor.PointerMove(X(1.9));
        _editor.PointerUp(X(1.9));

        Assert.True(item.Start >= timeline.Items[0].End);
        Assert.True(item.Start - timeline.Items[0].End < TimeSpan.FromMilliseconds(TimelineEditor.GridMilliseconds));
        Assert.Equal(S(4), item.End);
    }

    [Theory]
    [InlineData(1890, 1900)]   // 40 ms past a step: the nearest step is the next one
    [InlineData(1870, 1850)]
    [InlineData(1850, 1850)]
    [InlineData(1875, 1900)]   // halfway rounds up
    public void The_playhead_pulls_an_edge_to_the_nearest_multiple_of_50_ms(int playheadMs, int expectedMs)
    {
        var timeline = Track((5, 1));
        var item = timeline.Items[0];
        _editor.SetPlayhead(TimeSpan.FromMilliseconds(playheadMs));

        // Drag the left edge to within a few pixels of the playhead.
        _editor.PointerDown(X(5) + 1, RowY(0));
        _editor.PointerMove(X(playheadMs / 1000.0) + 3);
        _editor.PointerUp(X(playheadMs / 1000.0) + 3);

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), item.Start);
        Assert.Equal(S(6), item.End);
    }

    [Fact]
    public void The_pull_of_the_playhead_is_measured_to_the_step_it_pulls_to()
    {
        var timeline = Track((5, 1));
        var item = timeline.Items[0];
        _editor.SetPlayhead(TimeSpan.FromMilliseconds(1870));   // pulls to 1850; at 100 px/s that reaches 80 ms

        _editor.PointerDown(X(5) + 1, RowY(0));
        _editor.PointerMove(X(1.95));   // the edge is at 1.94 (the grab was 10 ms inside): 90 ms past 1850, out of reach
        _editor.PointerUp(X(1.95));

        Assert.Equal(TimeSpan.FromMilliseconds(1950), item.Start);
    }

    [Fact]
    public void A_marker_pulls_an_edge_to_the_nearest_multiple_of_50_ms_too()
    {
        var timeline = Track((5, 1));
        var item = timeline.Items[0];
        _doc.Execute(PixelTuner.Core.Editing.Edits.AddMarker(_doc.Project, new Marker { Name = "m", Position = TimeSpan.FromMilliseconds(2870) }));

        _editor.PointerDown(X(5) + 1, RowY(0));
        _editor.PointerMove(X(2.87) - 3);
        _editor.PointerUp(X(2.87) - 3);

        Assert.Equal(TimeSpan.FromMilliseconds(2850), item.Start);
    }

    [Fact]
    public void Edges_stay_a_grid_step_apart_at_least()
    {
        var timeline = Track((2, 1));
        var item = timeline.Items[0];

        _editor.PointerDown(X(3) - 1, RowY(0));
        _editor.PointerMove(X(2));   // all the way over the start
        _editor.PointerUp(X(2));

        Assert.Equal(TimeSpan.FromMilliseconds(TimelineEditor.GridMilliseconds), item.Duration);
        Assert.True(OnGrid(item.End));
    }

    [Fact]
    public void A_new_item_starts_on_the_grid_when_the_playhead_is_between_steps()
    {
        var timeline = Track();
        _editor.SetPlayhead(TimeSpan.FromMilliseconds(1234.5));
        var item = _editor.AddItem(timeline, new TimelineItem(), S(5));

        Assert.NotNull(item);
        Assert.Equal(TimeSpan.FromMilliseconds(1250), item!.Start);
        Assert.Equal(S(5), item.Duration);
    }

    [Fact]
    public void A_new_item_cut_by_the_next_one_ends_on_the_grid()
    {
        var timeline = Track((3.0, 1));
        _editor.SetPlayhead(TimeSpan.FromMilliseconds(1234));
        var item = _editor.AddItem(timeline, new TimelineItem(), S(5));

        Assert.True(OnGrid(item!.Start) && OnGrid(item.End));
        Assert.Equal(S(3), item.End);
    }

    [Fact]
    public void Dragging_the_edges_trims_and_resizes()
    {
        var timeline = Track((1, 4), (7, 1));
        var item = timeline.Items[0];

        _editor.PointerDown(X(5) - 1, RowY(0));              // the end
        Assert.Equal(HitKind.ItemEnd, _editor.HitTest(X(5) - 1, RowY(0)).Kind);
        _editor.PointerUp(X(5) - 1 + 150);
        Assert.Equal(S(1), item.Start);
        AssertNear(6.5, item.End);

        _editor.PointerDown(X(1) + 1, RowY(0));              // the start
        _editor.PointerUp(X(1) + 1 + 100);
        AssertNear(2, item.Start);
        AssertNear(6.5, item.End);

        _editor.PointerDown(X(6.5) - 1, RowY(0));            // cannot run into the next item
        _editor.PointerUp(X(6.5) - 1 + 2000);
        Assert.Equal(S(7), item.End);
    }

    [Fact]
    public void An_item_cannot_be_dragged_shorter_than_the_minimum()
    {
        var timeline = Track((1, 1));
        var item = timeline.Items[0];
        _editor.PointerDown(X(2) - 1, RowY(0));
        _editor.PointerUp(X(2) - 1 - 5000);
        Assert.True(item.Duration >= S(0.02));
        Assert.Equal(S(1), item.Start);
    }

    [Fact]
    public void Markers_drag_and_reorder()
    {
        var a = new Marker { Name = "a", Position = S(2) };
        var b = new Marker { Name = "b", Position = S(5) };
        _doc.Project.Markers.AddRange([a, b]);

        _editor.PointerDown(X(2), 10);
        _editor.PointerUp(X(2) + 600);

        Assert.Equal(S(8), a.Position);
        Assert.Equal(["b", "a"], _doc.Project.Markers.Select(m => m.Name));
        _doc.Undo();
        Assert.Equal(["a", "b"], _doc.Project.Markers.Select(m => m.Name));
    }

    [Fact]
    public void Delete_removes_the_selection_and_undo_brings_it_back()
    {
        var timeline = Track((1, 1));
        _editor.PointerDown(X(1.5), RowY(0));
        _editor.PointerUp(X(1.5));

        _editor.DeleteSelected();
        Assert.Empty(timeline.Items);
        Assert.Null(_editor.Selected);

        _doc.Undo();
        Assert.Single(timeline.Items);

        _editor.Select(timeline);
        _editor.DeleteSelected();
        Assert.Empty(_doc.Project.Timelines);
        _doc.Undo();
        Assert.Single(_doc.Project.Timelines);
    }

    [Fact]
    public void Add_marker_uses_the_playhead_and_add_timeline_selects_the_new_one()
    {
        _editor.SetPlayhead(S(12.5));
        var marker = _editor.AddMarkerAtPlayhead("Marker 1");
        Assert.Equal(S(12.5), marker.Position);
        Assert.Same(marker, _editor.Selected);

        var timeline = _editor.AddTimeline("Track 1", TimelineKind.Pixel, "#FF0000FF");
        Assert.Same(timeline, _editor.Selected);
        Assert.Single(_doc.Project.Timelines);
        Assert.Equal("AddTimeline", _doc.UndoKey);
    }

    [Fact]
    public void Changed_is_raised_for_view_selection_and_project_changes()
    {
        int raised = 0;
        _editor.Changed += (_, _) => raised++;
        Track((0, 1));

        _editor.ZoomAt(2, 200);
        _editor.SetPlayhead(S(1));
        _editor.Select(_doc.Project.Timelines[0]);
        _doc.Name = "Renamed";

        Assert.True(raised >= 4);
    }

    [Fact]
    public void Item_dragged_down_lands_on_the_next_timeline_of_its_kind_and_undo_returns_it()
    {
        var a = Track((1, 2));
        var b = Track((6, 1));
        var other = Track();
        other.Kind = TimelineKind.Video;
        var item = a.Items[0];

        _editor.PointerDown(X(2), RowY(0));
        _editor.PointerMove(X(2), RowY(1));
        Assert.Same(b, _editor.Preview!.Timeline);
        _editor.PointerUp(X(2), RowY(1));

        Assert.Empty(a.Items);
        Assert.Equal([item.Start, S(6)], b.Items.Select(i => i.Start).Order());
        Assert.Contains(item, b.Items);

        _doc.Undo();
        Assert.Contains(item, a.Items);
        Assert.DoesNotContain(item, b.Items);
    }

    [Fact]
    public void Item_stays_when_dragged_over_another_kind_or_over_an_item()
    {
        var a = Track((1, 2));
        var busy = Track((0, 10));
        var video = Track();
        video.Kind = TimelineKind.Video;
        var item = a.Items[0];

        _editor.PointerDown(X(2), RowY(0));
        _editor.PointerMove(X(2), RowY(1));
        Assert.Same(a, _editor.Preview!.Timeline);
        _editor.PointerMove(X(2), RowY(2));
        Assert.Same(a, _editor.Preview!.Timeline);
        _editor.PointerUp(X(2), RowY(2));
        Assert.Contains(item, a.Items);
    }

    [Fact]
    public void Dragging_over_a_timeline_of_another_kind_says_why_it_will_not_go_there()
    {
        var a = Track((1, 2));
        var video = Track();
        video.Kind = TimelineKind.Video;

        _editor.PointerDown(X(2), RowY(0));
        _editor.PointerMove(X(2), RowY(1));

        var denial = _editor.Preview!.Denial!;
        Assert.Same(video, denial.Target);
        Assert.Equal(DropDenialReason.WrongKind, denial.Reason);
        Assert.Same(a, _editor.Preview.Timeline);
        Assert.Contains("Video", denial.Text(), StringComparison.OrdinalIgnoreCase);

        _editor.PointerMove(X(2), RowY(0));   // back over its own timeline: no complaint
        Assert.Null(_editor.Preview!.Denial);
    }

    [Fact]
    public void Dragging_a_picture_over_a_pixel_timeline_of_another_height_says_both_heights()
    {
        var a = Track((1, 2));
        var tall = Track();
        a.Items[0].MediaHeight = 36;
        tall.Items.Add(new TimelineItem { Start = S(10), Duration = S(1), MediaHeight = 80 });

        _editor.PointerDown(X(2), RowY(0));
        _editor.PointerMove(X(2), RowY(1));

        var denial = _editor.Preview!.Denial!;
        Assert.Equal((DropDenialReason.WrongHeight, 80, 36), (denial.Reason, denial.RequiredHeight, denial.ItemHeight));
        var text = denial.Text();
        Assert.Contains("80", text);
        Assert.Contains("36", text);
        _editor.PointerUp(X(2), RowY(1));
        Assert.Contains(a.Items[0], a.Items);   // it stayed
    }

    [Fact]
    public void Dragging_over_a_timeline_with_no_room_at_that_time_says_so()
    {
        var a = Track((1, 2));
        Track((0, 10));

        _editor.PointerDown(X(2), RowY(0));
        _editor.PointerMove(X(2), RowY(1));

        Assert.Equal(DropDenialReason.NoRoom, _editor.Preview!.Denial!.Reason);
    }

    [Theory]
    [InlineData("1:05.250", 65.25)]
    [InlineData("65.25", 65.25)]
    [InlineData("65,25", 65.25)]
    [InlineData(" 0:03 ", 3)]
    [InlineData("12", 12)]
    public void Time_text_is_read_back(string text, double seconds)
    {
        Assert.True(TimeFormat.TryParse(text, out var time));
        Assert.Equal(seconds, time.TotalSeconds, 6);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1:2:3")]
    [InlineData("-5")]
    [InlineData("1:-5")]
    public void Bad_time_text_is_refused(string text) => Assert.False(TimeFormat.TryParse(text, out _));
}
