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

    private static TimelineItem Picture(int height, double start = 0) =>
        new() { Kind = ItemKind.Image, Start = S(start), Duration = S(1), MediaWidth = 10, MediaHeight = height };

    [Fact]
    public void A_pixel_timeline_takes_pictures_of_the_height_of_its_first()
    {
        var timeline = new Timeline();
        _history.Do(Edits.AddItem(timeline, Picture(36)));
        Assert.Equal(36, Edits.PixelHeight(timeline));

        _history.Do(Edits.AddItem(timeline, Picture(36, start: 2)));
        Assert.Throws<InvalidOperationException>(() => Edits.AddItem(timeline, Picture(80, start: 4)));
        Assert.Equal(2, timeline.Items.Count);
    }

    [Fact]
    public void The_height_of_a_timeline_is_its_own_before_it_has_pictures()
    {
        var timeline = new Timeline();
        _history.Do(Edits.SetTimelineHeight(timeline, 125));

        Assert.False(Edits.FitsHeight(timeline, 36));
        Assert.True(Edits.FitsHeight(timeline, 125));
        Assert.Throws<InvalidOperationException>(() => Edits.AddItem(timeline, Picture(36)));
        _history.Do(Edits.AddItem(timeline, Picture(125)));

        Assert.Throws<InvalidOperationException>(() => Edits.SetTimelineHeight(timeline, 80));
        _history.Do(Edits.SetTimelineHeight(timeline, 0));   // back to "the first picture decides"
        Assert.Equal(125, Edits.PixelHeight(timeline));

        _history.Undo();
        Assert.Equal(125, timeline.Height);
    }

    [Fact]
    public void A_picture_of_unknown_height_is_not_held_to_the_rule()
    {
        // Older packages did not store the size.
        var timeline = new Timeline();
        _history.Do(Edits.AddItem(timeline, Picture(36)));
        _history.Do(Edits.AddItem(timeline, Picture(0, start: 2)));
        Assert.Equal(2, timeline.Items.Count);
        Assert.Equal(36, Edits.PixelHeight(timeline));
    }

    [Fact]
    public void An_item_moves_only_to_a_timeline_of_its_height()
    {
        var from = new Timeline();
        var same = new Timeline();
        var other = new Timeline();
        var item = Picture(36);
        _history.Do(Edits.AddItem(from, item));
        _history.Do(Edits.AddItem(same, Picture(36)));
        _history.Do(Edits.AddItem(other, Picture(80)));

        Assert.Throws<InvalidOperationException>(() => Edits.MoveItemToTimeline(from, other, item, S(5)));
        _history.Do(Edits.MoveItemToTimeline(from, same, item, S(5)));
        Assert.Equal(2, same.Items.Count);
    }

    [Fact]
    public void Only_a_pixel_timeline_has_a_height()
    {
        Assert.Equal(0, Edits.PixelHeight(new Timeline { Kind = TimelineKind.Video }));
        Assert.Throws<InvalidOperationException>(() => Edits.SetTimelineHeight(new Timeline { Kind = TimelineKind.Light }, 10));
    }

    [Fact]
    public void Times_of_items_are_whole_milliseconds()
    {
        var timeline = new Timeline();
        var half = TimeSpan.FromTicks(TimeSpan.TicksPerMillisecond / 2);
        Assert.Throws<ArgumentException>(() => Edits.AddItem(timeline, new TimelineItem { Start = S(1) + half, Duration = S(1) }));
        Assert.Throws<ArgumentException>(() => Edits.AddItem(timeline, new TimelineItem { Start = S(1), Duration = S(1) + half }));

        var item = new TimelineItem { Start = S(1), Duration = S(1) };
        _history.Do(Edits.AddItem(timeline, item));
        Assert.Throws<ArgumentException>(() => Edits.MoveItem(timeline, item, S(3) + half));
        Assert.Throws<ArgumentException>(() => Edits.ResizeItem(timeline, item, S(2) + half));
        Assert.Throws<ArgumentException>(() => Edits.TrimItemStart(timeline, item, S(1) + half));
        Assert.Throws<ArgumentException>(() => Edits.MoveItemToTimeline(timeline, new Timeline(), item, S(3) + half));
        Assert.Equal((S(1), S(1)), (item.Start, item.Duration));
    }

    [Theory]
    [InlineData(10_004_000, 10_000_000)]
    [InlineData(10_005_000, 10_010_000)]
    [InlineData(10_016_000, 10_020_000)]
    public void WholeMs_rounds_to_the_nearest_millisecond(long ticks, long expected)
    {
        Assert.Equal(TimeSpan.FromTicks(expected), Edits.WholeMs(TimeSpan.FromTicks(ticks)));
    }

    [Fact]
    public void Build_settings_start_as_in_PixelTuner_2_and_change_with_undo()
    {
        Assert.Equal((Export.ColorDepth.Bit24, 4, 5), (_project.Build.Depth, _project.Build.GapStretching, _project.Build.LightRate));

        _history.Do(Edits.SetColorDepth(_project, Export.ColorDepth.Bit36));
        _history.Do(Edits.SetGapStretching(_project, 8));
        _history.Do(Edits.SetLightRate(_project, 10));
        Assert.Equal((Export.ColorDepth.Bit36, 8, 10), (_project.Build.Depth, _project.Build.GapStretching, _project.Build.LightRate));

        while (_history.Undo()) { }
        Assert.Equal((Export.ColorDepth.Bit24, 4, 5), (_project.Build.Depth, _project.Build.GapStretching, _project.Build.LightRate));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(Edits.MaxEpicStretching + 1)]
    public void Pauses_cannot_be_stretched_below_the_least_or_above_what_E_Pic_stores(int stretching)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Edits.SetGapStretching(_project, stretching));
        Assert.Throws<ArgumentOutOfRangeException>(() => Edits.SetLightRate(_project, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Edits.SetLightRate(_project, Edits.MaxLightRate + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Edits.SetColorDepth(_project, (Export.ColorDepth)7));
    }

    [Fact]
    public void A_property_the_bin_has_no_field_for_is_refused()
    {
        var picture = new TimelineItem { Kind = ItemKind.Image };
        var colour = new TimelineItem { Kind = ItemKind.Solid, Color = "#FFFFFF" };
        var effect = new TimelineItem { Kind = ItemKind.Effect };

        Assert.Throws<InvalidOperationException>(() => Edits.SetOffset(picture, 1, 1));          // video only
        Assert.Throws<InvalidOperationException>(() => Edits.SetLaser(colour, true));            // light has no laser
        Assert.Throws<InvalidOperationException>(() => Edits.SetBrightness(effect, 50));
        Assert.Throws<InvalidOperationException>(() => Edits.SetStretching(colour, 4));
        Assert.Throws<InvalidOperationException>(() => Edits.SetItemColor(picture, "#FFFFFF"));
        Assert.Throws<InvalidOperationException>(() => Edits.SetItemColorLast(colour, "#FFFFFF"));   // a solid has one colour
        Assert.Throws<InvalidOperationException>(() => Edits.SetEffect(colour, LightEffectKind.SineWave));
        Assert.Throws<InvalidOperationException>(() => Edits.SetEffectTimes(picture, 1, 1));
    }

    [Theory]
    [InlineData(ItemKind.Image, 65535, true)]
    [InlineData(ItemKind.Image, 65536, false)]
    [InlineData(ItemKind.Video, 40, true)]
    [InlineData(ItemKind.Gradient, Edits.MaxEpicStretching, true)]
    [InlineData(ItemKind.Gradient, Edits.MaxEpicStretching + 1, false)]   // the file keeps it times 5 in 16 bits
    [InlineData(ItemKind.Image, 0, false)]
    [InlineData(ItemKind.Image, 1, false)]   // 2 is the least, for every kind of device
    [InlineData(ItemKind.Image, 2, true)]
    [InlineData(ItemKind.Video, 1, false)]
    [InlineData(ItemKind.Video, 2, true)]
    [InlineData(ItemKind.Gradient, 1, false)]
    [InlineData(ItemKind.Gradient, 2, true)]
    public void Stretching_stays_inside_what_the_bin_can_store(string kind, int stretching, bool allowed)
    {
        var item = new TimelineItem { Kind = kind };
        if (allowed) _history.Do(Edits.SetStretching(item, stretching));
        else Assert.Throws<ArgumentOutOfRangeException>(() => Edits.SetStretching(item, stretching));
    }

    [Fact]
    public void Flip_monochrome_and_layout_belong_to_their_kind_of_timeline()
    {
        var pixel = new Timeline { Kind = TimelineKind.Pixel };
        var light = new Timeline { Kind = TimelineKind.Light };
        var video = new Timeline { Kind = TimelineKind.Video };
        var layout = new LedLayout { Columns = 2, Rows = 2, Leds = [0, 1] };
        _project.LedLayouts.Add(layout);

        _history.Do(Edits.SetFlipVertical(pixel, true));
        _history.Do(Edits.SetMonochrome(light, true));
        _history.Do(Edits.SetLedLayout(_project, video, layout.Id));
        Assert.True(pixel.FlipVertical);
        Assert.True(light.Monochrome);
        Assert.Equal(layout.Id, video.LedLayoutId);

        Assert.Throws<InvalidOperationException>(() => Edits.SetFlipVertical(video, true));
        Assert.Throws<InvalidOperationException>(() => Edits.SetMonochrome(pixel, true));
        Assert.Throws<InvalidOperationException>(() => Edits.SetLedLayout(_project, pixel, layout.Id));
        Assert.Throws<ArgumentException>(() => Edits.SetLedLayout(_project, video, Guid.NewGuid()));

        _history.Do(Edits.SetLedLayout(_project, video, null));
        Assert.Null(video.LedLayoutId);
        while (_history.Undo()) { }
        Assert.False(pixel.FlipVertical);
        Assert.False(light.Monochrome);
    }

    [Fact]
    public void Replacing_a_picture_keeps_the_timeline_height_and_undoes()
    {
        var timeline = new Timeline();
        var first = Picture(36);
        var second = Picture(36, start: 2);
        _history.Do(Edits.AddItem(timeline, first));
        _history.Do(Edits.AddItem(timeline, second));

        Assert.Throws<InvalidOperationException>(() => Edits.ReplaceMedia(timeline, second, "b", "b.png", 10, 80));
        // The only picture of a timeline may change height, as nothing else is held to it.
        var lone = new Timeline();
        var only = Picture(36);
        _history.Do(Edits.AddItem(lone, only));
        _history.Do(Edits.ReplaceMedia(lone, only, "c", "c.png", 7, 80));
        Assert.Equal((80, "c"), (only.MediaHeight, only.ResourceId));

        _history.Do(Edits.ReplaceMedia(timeline, second, "b", "b.png", 12, 36));
        Assert.Equal(("b", "b.png", 12), (second.ResourceId, second.SourcePath, second.MediaWidth));
        _history.Undo();
        Assert.Equal(10, second.MediaWidth);
    }

    private TimelineItem Pic(double start, int stretching = 4, byte brightness = 100, bool laser = false) =>
        new() { Kind = ItemKind.Image, Start = S(start), Duration = S(1), Stretching = stretching, Brightness = brightness, LaserOn = laser };

    [Fact]
    public void Settings_of_one_item_go_to_the_others_in_one_undo_step()
    {
        var timeline = new Timeline();
        var source = Pic(0, stretching: 9, brightness: 40, laser: true);
        var a = Pic(2);
        var b = Pic(4, stretching: 9);   // partly the same already
        foreach (var item in new[] { source, a, b }) _history.Do(Edits.AddItem(timeline, item));

        _history.Do(Edits.ApplySettings(source, [source, a, b])!);

        Assert.All(new[] { a, b }, i => Assert.Equal((9, (byte)40, true), (i.Stretching, i.Brightness, i.LaserOn)));
        _history.Undo();
        Assert.Equal((4, (byte)100, false), (a.Stretching, a.Brightness, a.LaserOn));
        Assert.Equal((9, (byte)100, false), (b.Stretching, b.Brightness, b.LaserOn));
        Assert.Equal("AddItem", _history.UndoKey);   // one step took the whole application back
    }

    [Fact]
    public void Settings_go_only_to_items_of_the_same_kind_and_nothing_to_change_is_no_edit()
    {
        var timeline = new Timeline();
        var source = Pic(0, stretching: 9);
        var other = new TimelineItem { Kind = ItemKind.Video, Start = S(2), Duration = S(1), Stretching = 40 };
        var same = Pic(4, stretching: 9);
        foreach (var item in new[] { source, same }) _history.Do(Edits.AddItem(timeline, item));

        Assert.Null(Edits.ApplySettings(source, [other, same]));   // the video is left alone, the picture has it all
        Assert.Equal(40, other.Stretching);
    }

    [Fact]
    public void Offsets_of_videos_and_colours_and_effects_of_lights_are_applied_too()
    {
        var video = new TimelineItem { Kind = ItemKind.Video, OffsetX = 5, OffsetY = 7, Stretching = 40 };
        var video2 = new TimelineItem { Kind = ItemKind.Video, Stretching = 40 };
        _history.Do(Edits.ApplySettings(video, [video2])!);
        Assert.Equal((5, 7), (video2.OffsetX, video2.OffsetY));

        var effect = new TimelineItem { Kind = ItemKind.Effect, Color = "#112233", ColorLast = "#445566", Effect = LightEffectKind.SineWave, T1 = 300, T2 = 20 };
        var effect2 = new TimelineItem { Kind = ItemKind.Effect, Color = "#000000", ColorLast = "#FFFFFF", Effect = LightEffectKind.LinearTransition };
        _history.Do(Edits.ApplySettings(effect, [effect2])!);
        Assert.Equal(("#112233", "#445566", LightEffectKind.SineWave, 300, 20), (effect2.Color, effect2.ColorLast, effect2.Effect, effect2.T1, effect2.T2));

        var solid = new TimelineItem { Kind = ItemKind.Solid, Color = "#ABCDEF" };
        var solid2 = new TimelineItem { Kind = ItemKind.Solid, Color = "#000000" };
        _history.Do(Edits.ApplySettings(solid, [solid2])!);
        Assert.Equal("#ABCDEF", solid2.Color);
    }

    [Fact]
    public void Several_items_are_removed_in_one_step_and_come_back_in_their_places()
    {
        var a = TimelineWith((0, 1), (2, 1), (4, 1), (6, 1));
        var b = TimelineWith((0, 1), (2, 1));
        var gone = new[] { a.Items[0], a.Items[2], a.Items[3], b.Items[1] };

        _history.Do(Edits.RemoveItems(_project, gone));
        Assert.Equal([2.0], Starts(a));
        Assert.Equal([0.0], Starts(b));

        _history.Undo();
        Assert.Equal([0.0, 2.0, 4.0, 6.0], Starts(a));
        Assert.Equal([0.0, 2.0], Starts(b));
        Assert.Throws<InvalidOperationException>(() => Edits.RemoveItems(_project, [new TimelineItem()]));
    }

    [Fact]
    public void A_timeline_of_an_unknown_kind_is_refused()
    {
        Assert.Throws<ArgumentException>(() => Edits.AddTimeline(_project, new Timeline { Kind = "hologram" }));
        Assert.Empty(_project.Timelines);
    }

    [Fact]
    public void Removing_a_timeline_takes_it_off_its_devices_and_undo_puts_it_back_in_place()
    {
        var first = TimelineWith();
        var timeline = TimelineWith();
        var last = TimelineWith();
        var device = new DeviceBinding { Name = "poi", TimelineIds = [first.Id, timeline.Id, last.Id] };
        var other = new DeviceBinding { Name = "hoop", TimelineIds = [last.Id] };
        _project.Devices.AddRange([device, other]);

        _history.Do(Edits.RemoveTimeline(_project, timeline));
        Assert.Equal([first.Id, last.Id], device.TimelineIds);
        Assert.Equal([last.Id], other.TimelineIds);

        _history.Undo();
        Assert.Equal([first.Id, timeline.Id, last.Id], device.TimelineIds);
        Assert.Equal(3, _project.Timelines.Count);
    }

    private DeviceBinding Device(string type, int leds = 0, params Timeline[] timelines) =>
        new() { Name = type, DeviceType = type, LedCount = leds, TimelineIds = timelines.Select(t => t.Id).ToList() };

    [Fact]
    public void A_device_plays_timelines_of_its_kind_in_the_order_chosen()
    {
        var a = TimelineWith();
        var b = TimelineWith();
        var device = Device(DeviceKind.Poi);
        _history.Do(Edits.AddDevice(_project, device));

        _history.Do(Edits.SetDeviceTimelines(_project, device, [b.Id, a.Id]));
        Assert.Equal([b.Id, a.Id], device.TimelineIds);
        _history.Do(Edits.SetDeviceTimelines(_project, device, [a.Id]));
        _history.Undo();
        Assert.Equal([b.Id, a.Id], device.TimelineIds);

        var light = new Timeline { Kind = TimelineKind.Light };
        _project.Timelines.Add(light);
        Assert.Throws<InvalidOperationException>(() => Edits.SetDeviceTimelines(_project, device, [a.Id, light.Id]));
        Assert.Throws<ArgumentException>(() => Edits.SetDeviceTimelines(_project, device, [a.Id, a.Id]));
        Assert.Throws<ArgumentException>(() => Edits.SetDeviceTimelines(_project, device, [Guid.NewGuid()]));

        _history.Do(Edits.RemoveDevice(_project, device));
        Assert.Empty(_project.Devices);
        _history.Undo();
        Assert.Single(_project.Devices);
    }

    [Fact]
    public void A_fan_plays_one_timeline_and_a_video_module_at_most_two()
    {
        var pixel = TimelineWith();
        var pixel2 = TimelineWith();
        var videos = Enumerable.Range(0, 3).Select(_ => new Timeline { Kind = TimelineKind.Video }).ToList();
        _project.Timelines.AddRange(videos);

        var fan = Device(DeviceKind.Fan);
        _history.Do(Edits.AddDevice(_project, fan));
        Assert.Throws<InvalidOperationException>(() => Edits.SetDeviceTimelines(_project, fan, [pixel.Id, pixel2.Id]));

        var video = Device(DeviceKind.Video);
        _history.Do(Edits.AddDevice(_project, video));
        _history.Do(Edits.SetDeviceTimelines(_project, video, [videos[0].Id, videos[1].Id]));
        Assert.Throws<InvalidOperationException>(() => Edits.SetDeviceTimelines(_project, video, videos.Select(t => t.Id).ToList()));
    }

    [Fact]
    public void A_poi_plays_only_timelines_as_high_as_its_LEDs()
    {
        var tall = new Timeline();
        _history.Do(Edits.AddTimeline(_project, tall));
        _history.Do(Edits.AddItem(tall, Picture(80)));
        var short1 = new Timeline();
        _history.Do(Edits.AddTimeline(_project, short1));
        _history.Do(Edits.AddItem(short1, Picture(36)));

        var poi = Device(DeviceKind.Poi, leds: 36);
        _history.Do(Edits.AddDevice(_project, poi));
        Assert.Throws<InvalidOperationException>(() => Edits.SetDeviceTimelines(_project, poi, [tall.Id]));
        _history.Do(Edits.SetDeviceTimelines(_project, poi, [short1.Id]));

        // The number of LEDs cannot change under the timelines it plays.
        Assert.Throws<InvalidOperationException>(() => Edits.SetDeviceLedCount(_project, poi, 80));
        _history.Do(Edits.SetDeviceLedCount(_project, poi, 36));

        Assert.Throws<ArgumentException>(() => Edits.AddDevice(_project, new DeviceBinding { DeviceType = "toaster" }));
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
        item.Kind = ItemKind.Video;
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
