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

    [Theory]
    [InlineData(TimelineKind.Pixel, ItemKind.Image, true)]
    [InlineData(TimelineKind.Pixel, ItemKind.Video, false)]
    [InlineData(TimelineKind.Pixel, ItemKind.Gradient, false)]
    [InlineData(TimelineKind.Pixel, ItemKind.Solid, false)]
    [InlineData(TimelineKind.Video, ItemKind.Video, true)]
    [InlineData(TimelineKind.Video, ItemKind.Image, false)]
    [InlineData(TimelineKind.EpicPoi, ItemKind.Gradient, true)]
    [InlineData(TimelineKind.EpicPoi, ItemKind.Image, false)]
    [InlineData(TimelineKind.Light, ItemKind.Solid, true)]
    [InlineData(TimelineKind.Light, ItemKind.Effect, true)]
    [InlineData(TimelineKind.Light, ItemKind.Image, false)]
    public void A_timeline_takes_only_items_of_its_kind(string timelineKind, string itemKind, bool accepted)
    {
        var timeline = new Timeline { Kind = timelineKind };
        var item = new TimelineItem { Kind = itemKind, Duration = S(1) };

        if (accepted)
        {
            _history.Do(Edits.AddItem(timeline, item));
            Assert.Single(timeline.Items);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => Edits.AddItem(timeline, item));
            Assert.Empty(timeline.Items);
            Assert.False(_history.CanUndo);
        }
    }

    [Fact]
    public void A_timeline_of_an_unknown_kind_is_refused()
    {
        Assert.Throws<ArgumentException>(() => Edits.AddTimeline(_project, new Timeline { Kind = "hologram" }));
        Assert.Empty(_project.Timelines);
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
    public void Trimming_the_start_keeps_the_end_and_stops_at_the_previous_item()
    {
        var timeline = TimelineWith((0, 1), (2, 3));
        var second = timeline.Items[1];

        _history.Do(Edits.TrimItemStart(timeline, second, S(1.5)));
        Assert.Equal((S(1.5), S(5)), (second.Start, second.End));

        Assert.Throws<InvalidOperationException>(() => Edits.TrimItemStart(timeline, second, S(0.5)));   // into the first item
        Assert.Throws<InvalidOperationException>(() => Edits.TrimItemStart(timeline, second, S(5)));     // no length left

        _history.Undo();
        Assert.Equal((S(2), S(5)), (second.Start, second.End));
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

    [Fact]
    public void Item_moves_to_another_timeline_of_its_kind_in_one_undo_step()
    {
        var a = TimelineWith((0, 2));
        var b = TimelineWith((1, 1), (5, 1));
        var item = a.Items[0];
        item.Kind = ItemKind.Image;

        _history.Do(Edits.MoveItemToTimeline(a, b, item, S(2)));
        Assert.Empty(a.Items);
        Assert.Equal([1d, 2d, 5d], Starts(b));

        _history.Undo();
        Assert.Same(item, Assert.Single(a.Items));
        Assert.Equal(S(0), item.Start);
        Assert.Equal([1d, 5d], Starts(b));
    }

    [Fact]
    public void Item_does_not_move_to_a_timeline_of_another_kind_or_onto_an_item()
    {
        var pixel = TimelineWith((0, 2));
        var video = TimelineWith();
        video.Kind = TimelineKind.Video;
        var full = TimelineWith((0, 10));
        var item = pixel.Items[0];
        item.Kind = ItemKind.Image;

        Assert.False(Edits.Accepts(video, item));
        Assert.Throws<InvalidOperationException>(() => Edits.MoveItemToTimeline(pixel, video, item, S(0)));
        Assert.Throws<InvalidOperationException>(() => Edits.MoveItemToTimeline(pixel, full, item, S(1)));
    }

    [Fact]
    public void Laser_and_offset_change_with_undo()
    {
        var item = TimelineWith((0, 1)).Items[0];
        _history.Do(Edits.SetLaser(item, true));
        _history.Do(Edits.SetOffset(item, 3, 4));
        Assert.True(item.LaserOn);
        Assert.Equal((3, 4), (item.OffsetX, item.OffsetY));

        _history.Undo();
        _history.Undo();
        Assert.False(item.LaserOn);
        Assert.Equal((0, 0), (item.OffsetX, item.OffsetY));
    }

    [Fact]
    public void Light_item_colours_effect_and_times_change_with_undo_and_refuse_bad_values()
    {
        var item = TimelineWith((0, 1)).Items[0];
        item.Kind = ItemKind.Effect;
        item.Color = "#FF0000";
        item.Effect = LightEffectKind.SineWave;

        _history.Do(Edits.SetItemColor(item, "#00FF00"));
        _history.Do(Edits.SetItemColorLast(item, "#0000FF"));
        _history.Do(Edits.SetEffect(item, LightEffectKind.SquareWave));
        _history.Do(Edits.SetEffectTimes(item, 250, 500));
        Assert.Equal(("#00FF00", "#0000FF", LightEffectKind.SquareWave, 250, 500), (item.Color, item.ColorLast, item.Effect, item.T1, item.T2));

        Assert.Throws<InvalidDataException>(() => Edits.SetItemColor(item, "green"));
        Assert.Throws<ArgumentOutOfRangeException>(() => Edits.SetEffect(item, "explosion"));
        Assert.Throws<ArgumentOutOfRangeException>(() => Edits.SetEffectTimes(item, -1, 0));

        while (_history.Undo()) { }
        Assert.Equal(("#FF0000", null, LightEffectKind.SineWave, 0, 0), (item.Color, item.ColorLast, item.Effect, item.T1, item.T2));
    }
}
