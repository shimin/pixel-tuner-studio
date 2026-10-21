using PixelTuner.Core.Editing;
using PixelTuner.Core.Model;

namespace PixelTuner.Core.Tests.Editing;

public class EditsTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    private readonly ShowProject _project = new();
    private readonly UndoHistory _history = new();

    private Timeline TimelineWith(params (double start, double length)[] items)
    {
        var timeline = new Timeline { Name = "T" };
        foreach (var (start, length) in items)
            timeline.Items.Add(new TimelineItem { Start = S(start), Duration = S(length) });
        _project.Timelines.Add(timeline);
        return timeline;
    }

    private static double[] Starts(Timeline t) => t.Items.Select(i => i.Start.TotalSeconds).ToArray();

    [Fact]
    public void Timelines_add_remove_rename_and_reorder_with_undo()
    {
        var a = new Timeline { Name = "A" };
        var b = new Timeline { Name = "B" };
        _history.Do(Edits.AddTimeline(_project, a));
        _history.Do(Edits.AddTimeline(_project, b));
        _history.Do(Edits.MoveTimeline(_project, b, 0));
        Assert.Equal(["B", "A"], _project.Timelines.Select(t => t.Name));

        _history.Do(Edits.RenameTimeline(a, "A2"));
        _history.Do(Edits.SetTimelineColor(a, "#FF00FF00"));
        _history.Do(Edits.RemoveTimeline(_project, b));
        Assert.Equal(["A2"], _project.Timelines.Select(t => t.Name));

        while (_history.Undo()) { }
        Assert.Empty(_project.Timelines);
        Assert.Equal("A", a.Name);
        Assert.Equal("#FF0000FF", a.Color);
    }

    [Fact]
    public void Removing_a_timeline_unbinds_its_devices_and_undo_binds_them_again()
    {
        var timeline = TimelineWith();
        var device = new DeviceBinding { Name = "poi", TimelineId = timeline.Id };
        var other = new DeviceBinding { Name = "hoop", TimelineId = Guid.NewGuid() };
        _project.Devices.AddRange([device, other]);

        _history.Do(Edits.RemoveTimeline(_project, timeline));
        Assert.Null(device.TimelineId);
        Assert.NotNull(other.TimelineId);

        _history.Undo();
        Assert.Equal(timeline.Id, device.TimelineId);
        Assert.Single(_project.Timelines);
    }

    [Fact]
    public void Items_are_inserted_in_time_order()
    {
        var timeline = TimelineWith((0, 1), (5, 1));

        _history.Do(Edits.AddItem(timeline, new TimelineItem { Start = S(2), Duration = S(1) }));

        Assert.Equal([0, 2, 5], Starts(timeline));
        _history.Undo();
        Assert.Equal([0, 5], Starts(timeline));
    }

    [Theory]
    [InlineData(0.5, 1)]     // starts inside the first item
    [InlineData(-1, 1)]      // before zero
    [InlineData(1.5, 0)]     // no length
    [InlineData(4.5, 1)]     // runs into the second item
    public void Items_that_overlap_or_lie_outside_are_refused(double start, double length)
    {
        var timeline = TimelineWith((0, 1), (5, 1));
        Assert.False(Edits.Fits(timeline, S(start), S(length)));
        Assert.Throws<InvalidOperationException>(() =>
            Edits.AddItem(timeline, new TimelineItem { Start = S(start), Duration = S(length) }));
    }

    [Fact]
    public void Items_may_touch_end_to_start()
    {
        var timeline = TimelineWith((0, 1), (2, 1));
        Assert.True(Edits.Fits(timeline, S(1), S(1)));
    }

    [Fact]
    public void Moving_an_item_past_a_neighbour_keeps_the_list_sorted_and_undoes_cleanly()
    {
        var timeline = TimelineWith((0, 1), (2, 1), (4, 1));
        var first = timeline.Items[0];

        _history.Do(Edits.MoveItem(timeline, first, S(6)));
        Assert.Equal([2, 4, 6], Starts(timeline));
        Assert.Same(first, timeline.Items[2]);

        _history.Undo();
        Assert.Equal([0, 2, 4], Starts(timeline));
        Assert.Same(first, timeline.Items[0]);
        _history.Redo();
        Assert.Equal([2, 4, 6], Starts(timeline));
    }

    [Fact]
    public void An_item_does_not_block_itself_and_cannot_move_onto_another()
    {
        var timeline = TimelineWith((0, 2), (3, 1));
        var first = timeline.Items[0];

        _history.Do(Edits.MoveItem(timeline, first, S(0.5)));          // overlaps only itself: fine
        Assert.Throws<InvalidOperationException>(() => Edits.MoveItem(timeline, first, S(2.5)));
        Assert.Equal([0.5, 3], Starts(timeline));
    }

    [Fact]
    public void Resize_stops_at_the_next_item()
    {
        var timeline = TimelineWith((0, 1), (2, 1));
        var first = timeline.Items[0];

        _history.Do(Edits.ResizeItem(timeline, first, S(2)));
        Assert.Equal(S(2), first.End);
        Assert.Throws<InvalidOperationException>(() => Edits.ResizeItem(timeline, first, S(2.5)));
        Assert.Throws<InvalidOperationException>(() => Edits.ResizeItem(timeline, first, TimeSpan.Zero));

        _history.Undo();
        Assert.Equal(S(1), first.Duration);
    }

    [Fact]
    public void Item_properties_are_validated_and_undone()
    {
        var item = new TimelineItem();
        _history.Do(Edits.SetBrightness(item, 40));
        _history.Do(Edits.SetStretching(item, 3));
        Assert.Equal((40, 3), (item.Brightness, item.Stretching));
        Assert.Throws<ArgumentOutOfRangeException>(() => Edits.SetBrightness(item, 101));
        Assert.Throws<ArgumentOutOfRangeException>(() => Edits.SetStretching(item, 0));

        _history.Undo();
        _history.Undo();
        Assert.Equal((100, 1), (item.Brightness, item.Stretching));
    }

    [Fact]
    public void Removing_an_item_restores_it_at_the_same_place()
    {
        var timeline = TimelineWith((0, 1), (2, 1), (4, 1));
        var middle = timeline.Items[1];

        _history.Do(Edits.RemoveItem(timeline, middle));
        Assert.Equal([0, 4], Starts(timeline));

        _history.Undo();
        Assert.Same(middle, timeline.Items[1]);
        Assert.Throws<InvalidOperationException>(() => Edits.RemoveItem(timeline, new TimelineItem()));
    }

    [Fact]
    public void Markers_stay_in_order_when_added_and_moved()
    {
        var late = new Marker { Name = "late", Position = S(9) };
        var early = new Marker { Name = "early", Position = S(1) };
        _history.Do(Edits.AddMarker(_project, late));
        _history.Do(Edits.AddMarker(_project, early));
        Assert.Equal(["early", "late"], _project.Markers.Select(m => m.Name));

        _history.Do(Edits.MoveMarker(_project, early, S(20)));
        Assert.Equal(["late", "early"], _project.Markers.Select(m => m.Name));

        _history.Do(Edits.RenameMarker(late, "drop"));
        _history.Do(Edits.RemoveMarker(_project, late));
        Assert.Equal(["early"], _project.Markers.Select(m => m.Name));

        while (_history.Undo()) { }
        Assert.Empty(_project.Markers);
        Assert.Equal("late", late.Name);
        Assert.Equal(S(1), early.Position);
    }

    [Fact]
    public void A_group_is_one_undo_step()
    {
        var timeline = TimelineWith();
        var edit = Edits.Group("Paste",
            Edits.AddItem(timeline, new TimelineItem { Start = S(0), Duration = S(1) }),
            Edits.AddMarker(_project, new Marker { Name = "m" }));

        _history.Do(edit);
        Assert.Single(timeline.Items);
        _history.Undo();

        Assert.Empty(timeline.Items);
        Assert.Empty(_project.Markers);
        Assert.False(_history.CanUndo);
    }
}
