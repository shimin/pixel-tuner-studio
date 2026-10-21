using PixelTuner.Core.Editing;
using PixelTuner.Core.Export;
using PixelTuner.Core.Imaging;
using PixelTuner.Core.Model;
using SkiaSharp;

namespace PixelTuner.Core.Tests;

/// <summary>
/// <see cref="ShowBuilder"/> is the one way a project becomes a .bin. The exports under it are checked against
/// PixelTuner 2; here it is checked that it hands them the right timelines and settings and stops what cannot be written.
/// </summary>
public class ShowBuilderTests
{
    private readonly ShowProject _project = new();
    private readonly UndoHistory _history = new();
    private readonly Dictionary<string, byte[]> _files = [];

    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    private ShowMedia Media => new(id => new MemoryStream(_files[id]));

    private BuildResult Build(DeviceBinding device) => new ShowBuilder(_project, Media).Build(device);

    private void Png(string name, int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        _files[name] = image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    private TimelineItem Picture(string name, int width, int height, double start, double length, int stretching = 4)
    {
        Png(name, width, height, new SKColor(200, 100, 50));
        return new TimelineItem
        {
            Kind = ItemKind.Image, ResourceId = name, SourcePath = name, MediaWidth = width, MediaHeight = height,
            Start = S(start), Duration = S(length), Stretching = stretching,
        };
    }

    private Timeline Add(string kind, params TimelineItem[] items)
    {
        var timeline = new Timeline { Kind = kind, Name = kind + _project.Timelines.Count };
        _history.Do(Edits.AddTimeline(_project, timeline));
        foreach (var item in items) _history.Do(Edits.AddItem(timeline, item));
        return timeline;
    }

    private DeviceBinding Bind(string deviceKind, int leds = 0, params Timeline[] timelines)
    {
        var device = new DeviceBinding { Name = deviceKind, DeviceType = deviceKind, LedCount = leds };
        _history.Do(Edits.AddDevice(_project, device));
        _history.Do(Edits.SetDeviceTimelines(_project, device, timelines.Select(t => t.Id).ToList()));
        return device;
    }

    private static bool Has(BuildResult result, BuildIssueCode code, BuildSeverity severity) =>
        result.Issues.Any(i => i.Code == code && i.Severity == severity);

    // ---- pixel devices

    [Fact]
    public void A_poi_gets_its_timelines_in_the_order_of_the_binding_with_the_settings_of_the_project()
    {
        var a = Add(TimelineKind.Pixel, Picture("a", 4, 4, 1, 1));
        var b = Add(TimelineKind.Pixel, Picture("b", 3, 4, 0, 2));
        var device = Bind(DeviceKind.Poi, 4, b, a);

        var result = Build(device);

        Assert.True(result.Succeeded);
        var file = BinReader.ReadPpoi(result.Bin!);
        Assert.Equal(4, file.Height);
        // b first (one picture, two seconds); a second: a pause of one second, then its picture.
        Assert.Equal(2, file.Timelines.Count);
        Assert.Single(file.Timelines[0].Entries);
        Assert.Equal(2, file.Timelines[1].Entries.Count);
        Assert.Equal(1000u, file.Timelines[1].Entries[1].StartMs);
        Assert.Equal<ushort>(4, file.Timelines[1].Entries[0].Stretching);   // PixelTuner 2's default for a pause

        // The same bytes as the export checked against PixelTuner 2.
        Assert.Equal(new PixelExport(Media).Export([b, a], 4), result.Bin);
    }

    [Fact]
    public void Colour_depth_and_pause_stretching_come_from_the_project_and_undo_with_it()
    {
        var timeline = Add(TimelineKind.Pixel, Picture("a", 4, 4, 1, 1));
        var device = Bind(DeviceKind.Poi, 4, timeline);
        byte[] before = Build(device).Bin!;

        _history.Do(Edits.SetColorDepth(_project, ColorDepth.Bit36));
        _history.Do(Edits.SetGapStretching(_project, 12));
        var file = BinReader.ReadPpoi(Build(device).Bin!);
        Assert.Equal(12, file.Timelines[0].Entries[0].Image.BitsPerChannel);   // 36-bit colour is 12 bits per channel
        Assert.Equal<ushort>(12, file.Timelines[0].Entries[0].Stretching);      // the pause

        _history.Undo();
        _history.Undo();
        Assert.Equal(before, Build(device).Bin);
    }

    [Fact]
    public void A_fan_plays_one_timeline_and_keeps_the_default_menu_colour()
    {
        var timeline = Add(TimelineKind.Pixel, Picture("a", 4, 7, 0, 1, stretching: 2));
        _history.Do(Edits.SetTimelineColor(timeline, "#FFFF0000"));
        var result = Build(Bind(DeviceKind.Fan, 0, timeline));

        var file = BinReader.ReadPpoi(result.Bin!);
        Assert.Equal(7, file.Height);
        Assert.Equal(new Rgb(0, 0, 20), file.Timelines[0].Color);
        // A fan counts milliseconds between columns: 2 is fine, a poi would refuse it.
        Assert.True(result.Succeeded);
        Assert.False(Build(Bind(DeviceKind.Poi, 7, timeline)).Succeeded);
    }

    [Fact]
    public void A_poi_does_not_play_a_stretching_below_four()
    {
        var timeline = Add(TimelineKind.Pixel, Picture("a", 4, 4, 0, 1, stretching: 3));
        var result = Build(Bind(DeviceKind.Poi, 4, timeline));

        Assert.False(result.Succeeded);
        var issue = Assert.Single(result.Errors);
        Assert.Equal(BuildIssueCode.StretchingTooSmall, issue.Code);
        Assert.Equal(timeline.Items[0].Id, issue.ItemId);
    }

    [Fact]
    public void A_timeline_of_another_height_than_the_poi_is_an_error_and_not_silently_left_out()
    {
        var tall = Add(TimelineKind.Pixel, Picture("a", 4, 80, 0, 1));
        var device = Bind(DeviceKind.Poi, 36);
        device.TimelineIds.Add(tall.Id);   // as an older project could have it

        var result = Build(device);
        Assert.False(result.Succeeded);
        var issue = Assert.Single(result.Errors);
        Assert.Equal((BuildIssueCode.WrongHeight, 80L), (issue.Code, issue.Detail));
    }

    [Fact]
    public void A_missing_picture_stops_the_build_and_names_the_item()
    {
        var item = Picture("a", 4, 4, 0, 1);
        var timeline = Add(TimelineKind.Pixel, item);
        item.ResourceId = null;   // the file was not found when the project was imported

        var result = Build(Bind(DeviceKind.Poi, 4, timeline));
        Assert.Null(result.Bin);
        Assert.Equal(item.Id, Assert.Single(result.Errors).ItemId);

        item.ResourceId = "gone";   // the package lost it
        Assert.Equal(BuildIssueCode.MissingMedia, Assert.Single(Build(Bind(DeviceKind.Poi, 4, timeline)).Errors).Code);
    }

    [Fact]
    public void A_file_that_does_not_fit_the_device_is_refused_with_its_size()
    {
        var timeline = Add(TimelineKind.Pixel, Picture("a", 400, 4, 0, 1));
        var device = Bind(DeviceKind.Poi, 4, timeline);
        device.MemoryBytes = 1000;

        var result = Build(device);
        Assert.False(result.Succeeded);
        var issue = Assert.Single(result.Errors);
        Assert.Equal(BuildIssueCode.TooBig, issue.Code);
        Assert.True(issue.Detail > 1000);

        device.MemoryBytes = issue.Detail!.Value;   // exactly the size fits
        Assert.True(Build(device).Succeeded);
    }

    [Fact]
    public void Nothing_to_play_and_empty_timelines_are_told_apart()
    {
        var empty = Add(TimelineKind.Pixel);
        Assert.Equal(BuildIssueCode.NothingToPlay, Assert.Single(Build(Bind(DeviceKind.Poi, 4, empty)).Errors).Code);

        var full = Add(TimelineKind.Pixel, Picture("a", 4, 4, 0, 1));
        var result = Build(Bind(DeviceKind.Poi, 4, empty, full));
        Assert.True(result.Succeeded);
        Assert.True(Has(result, BuildIssueCode.EmptyTimelineSkipped, BuildSeverity.Warning));
        Assert.Single(BinReader.ReadPpoi(result.Bin!).Timelines);
    }

    // ---- E-Pic

    [Fact]
    public void An_epic_poi_writes_its_stretching_times_five()
    {
        Png("g", 5, 1, new SKColor(10, 20, 30));
        var item = new TimelineItem { Kind = ItemKind.Gradient, ResourceId = "g", SourcePath = "g", Duration = S(1), Stretching = 4 };
        var timeline = Add(TimelineKind.EpicPoi, item);

        var file = BinReader.ReadPpoi(Build(Bind(DeviceKind.EpicPoi, 0, timeline)).Bin!);
        Assert.Equal(1, file.Height);
        Assert.Equal<ushort>(20, file.Timelines[0].Entries[0].Stretching);
        Assert.Equal(5, file.Timelines[0].Entries[0].Image.Width);

        item.Stretching = Edits.MaxEpicStretching + 1;
        Assert.Equal(BuildIssueCode.StretchingTooLarge, Assert.Single(Build(Bind(DeviceKind.EpicPoi, 0, timeline)).Errors).Code);
    }

    // ---- LightMaster

    private static TimelineItem Colour(string color, double start, double length) =>
        new() { Kind = ItemKind.Solid, Color = color, Start = S(start), Duration = S(length) };

    [Fact]
    public void The_light_step_of_the_project_is_the_time_between_samples()
    {
        var timeline = Add(TimelineKind.Light, Colour("#FF0000", 0, 0.1), Colour("#00FF00", 0.1, 0.1));
        var device = Bind(DeviceKind.Light, 0, timeline);

        Assert.Equal([0u, 100u, 200u], BinReader.ReadLght(Build(device).Bin!).Samples.Select(s => s.TimeMs));

        _history.Do(Edits.SetLightRate(_project, 20));
        var file = BinReader.ReadLght(Build(device).Bin!);
        Assert.Equal([0u, 100u, 200u], file.Samples.Select(s => s.TimeMs));   // multiples of 20 as well
        Assert.All(file.Samples, s => Assert.Equal(0u, s.TimeMs % 20));
    }

    [Fact]
    public void An_item_that_is_not_a_whole_number_of_light_steps_is_a_warning()
    {
        var odd = Colour("#FF0000", 0, 0.103);
        var timeline = Add(TimelineKind.Light, odd);

        var result = Build(Bind(DeviceKind.Light, 0, timeline));
        Assert.True(result.Succeeded);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal((BuildIssueCode.LightStepNotMultiple, odd.Id), (warning.Code, warning.ItemId));
    }

    [Fact]
    public void An_unknown_light_effect_is_an_error()
    {
        var effect = new TimelineItem { Kind = ItemKind.Effect, Effect = "disco", Color = "#000000", ColorLast = "#FFFFFF", Duration = S(1) };
        var timeline = Add(TimelineKind.Light);
        timeline.Items.Add(effect);   // an imported project can have what the editor would refuse

        Assert.Equal(BuildIssueCode.UnknownEffect, Assert.Single(Build(Bind(DeviceKind.Light, 0, timeline)).Errors).Code);
    }

    // ---- video

    private static string Gif() => Path.Combine(AppContext.BaseDirectory, "Fixtures", "LegacyProjects", "glasses", "glasses.gif");

    private LedLayout Layout(int columns, int rows, params int[] leds)
    {
        var layout = new LedLayout { Name = "prop", Columns = columns, Rows = rows, Leds = leds.ToList() };
        _project.LedLayouts.Add(layout);
        return layout;
    }

    private TimelineItem Video(double start, double length, int offsetX = 0)
    {
        _files["gif"] = File.ReadAllBytes(Gif());
        return new TimelineItem
        {
            Kind = ItemKind.Video, ResourceId = "gif", SourcePath = "glasses.gif", Start = S(start), Duration = S(length),
            Stretching = 40, OffsetX = offsetX,
        };
    }

    [Fact]
    public void A_video_module_gets_one_row_per_led_of_the_layout()
    {
        var layout = Layout(3, 2, 0, 1, 2, 5, 4, 3);
        var timeline = Add(TimelineKind.Video, Video(0, 2));
        _history.Do(Edits.SetLedLayout(_project, timeline, layout.Id));

        var result = Build(Bind(DeviceKind.Video, 0, timeline));
        var file = BinReader.ReadPpoi(result.Bin!);
        Assert.Equal(6, file.Height);
        Assert.Equal<ushort>(40, file.Timelines[0].Entries[0].Stretching);   // 40 ms between frames: 25 frames a second
        Assert.True(file.Timelines[0].Entries[0].Image.Width > 1);           // one column per frame
    }

    [Fact]
    public void A_video_timeline_without_markup_is_an_error_before_the_build()
    {
        var timeline = Add(TimelineKind.Video, Video(0, 2));
        Assert.Equal(BuildIssueCode.NoLedLayout, Assert.Single(Build(Bind(DeviceKind.Video, 0, timeline)).Errors).Code);
    }

    [Fact]
    public void Two_video_chains_must_end_together_and_make_one_timeline_of_both()
    {
        var left = Add(TimelineKind.Video, Video(0, 2));
        var right = Add(TimelineKind.Video, Video(0, 3));
        _history.Do(Edits.SetLedLayout(_project, left, Layout(2, 2, 0, 1).Id));
        _history.Do(Edits.SetLedLayout(_project, right, Layout(2, 2, 2, 3, 1).Id));
        var device = Bind(DeviceKind.Video, 0, left, right);

        Assert.Equal(BuildIssueCode.VideoPairEndsDiffer, Assert.Single(Build(device).Errors).Code);

        _history.Do(Edits.ResizeItem(left, left.Items[0], S(3)));
        var file = BinReader.ReadPpoi(Build(device).Bin!);
        Assert.Equal(5, file.Height);        // 2 + 3 LEDs
        Assert.Single(file.Timelines);       // a pair is one timeline in the file
    }

    [Fact]
    public void The_same_gif_at_another_offset_shares_the_first_picture_and_the_builder_says_so()
    {
        var first = Video(0, 1);
        var second = Video(1, 1, offsetX: 3);
        var timeline = Add(TimelineKind.Video, first, second);
        _history.Do(Edits.SetLedLayout(_project, timeline, Layout(2, 2, 0, 1, 2, 3).Id));

        var result = Build(Bind(DeviceKind.Video, 0, timeline));
        Assert.True(result.Succeeded);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal((BuildIssueCode.SharedBlockOffsets, second.Id), (warning.Code, warning.ItemId));
        Assert.Single(BinReader.Images(BinReader.ReadPpoi(result.Bin!)));
    }

    [Fact]
    public void Video_offset_layout_and_laser_reach_the_file_and_undo_brings_the_old_file_back()
    {
        var item = Video(0, 2);
        var timeline = Add(TimelineKind.Video, item);
        var small = Layout(2, 2, 0, 1);
        var big = Layout(3, 3, 0, 1, 2, 3);
        _history.Do(Edits.SetLedLayout(_project, timeline, small.Id));
        var device = Bind(DeviceKind.Video, 0, timeline);
        byte[] first = Build(device).Bin!;

        _history.Do(Edits.SetOffset(item, 40, 15));   // the markup sits elsewhere on the frame: other pixels
        byte[] moved = Build(device).Bin!;
        Assert.NotEqual(first, moved);
        Assert.Equal(first.Length, moved.Length);     // same size, other colours
        _history.Undo();
        Assert.Equal(first, Build(device).Bin);

        _history.Do(Edits.SetLedLayout(_project, timeline, big.Id));
        Assert.Equal(4, BinReader.ReadPpoi(Build(device).Bin!).Height);   // H follows the markup
        _history.Undo();
        Assert.Equal(2, BinReader.ReadPpoi(Build(device).Bin!).Height);

        Assert.Equal<ushort>(0, BinReader.ReadPpoi(first).Timelines[0].Entries[0].Mask);
        _history.Do(Edits.SetLaser(item, true));
        Assert.Equal<ushort>(1, BinReader.ReadPpoi(Build(device).Bin!).Timelines[0].Entries[0].Mask);   // video devices read bit 0 only
    }

    [Fact]
    public void The_video_colour_of_the_timeline_is_not_written_and_the_epic_colour_is_written_as_it_is()
    {
        var video = Add(TimelineKind.Video, Video(0, 1));
        _history.Do(Edits.SetLedLayout(_project, video, Layout(2, 1, 0, 1).Id));
        _history.Do(Edits.SetTimelineColor(video, "#FF00FF00"));
        Assert.Equal(new Rgb(0, 0, 20), BinReader.ReadPpoi(Build(Bind(DeviceKind.Video, 0, video)).Bin!).Timelines[0].Color);

        Png("g", 2, 1, new SKColor(1, 2, 3));
        var epic = Add(TimelineKind.EpicPoi, new TimelineItem { Kind = ItemKind.Gradient, ResourceId = "g", SourcePath = "g", Duration = S(1), Stretching = 4 });
        _history.Do(Edits.SetTimelineColor(epic, "#FF28B4DC"));
        var file = BinReader.ReadPpoi(Build(Bind(DeviceKind.EpicPoi, 0, epic)).Bin!);
        Assert.Equal(new Rgb(0x28, 0xB4, 0xDC), file.Timelines[0].Color);   // not divided by ten
        Assert.Equal(file.Timelines[0].Color, file.Timelines[0].SelectionColor);
    }

    [Fact]
    public void Replacing_the_picture_of_an_item_changes_its_block_and_nothing_else()
    {
        var item = Picture("a", 4, 4, 0, 1);
        var timeline = Add(TimelineKind.Pixel, item);
        var device = Bind(DeviceKind.Poi, 4, timeline);
        byte[] before = Build(device).Bin!;
        Png("b", 6, 4, new SKColor(9, 9, 9));

        _history.Do(Edits.ReplaceMedia(timeline, item, "b", "b.png", 6, 4));
        var after = BinReader.ReadPpoi(Build(device).Bin!);
        Assert.Equal(6, after.Timelines[0].Entries[0].Image.Width);
        Assert.Equal(4, BinReader.ReadPpoi(before).Timelines[0].Entries[0].Image.Width);
        _history.Undo();
        Assert.Equal(before, Build(device).Bin);
    }

    [Fact]
    public void The_order_of_light_timelines_is_the_order_of_the_slots()
    {
        var red = Add(TimelineKind.Light, Colour("#FF0000", 0, 1));
        var blue = Add(TimelineKind.Light, Colour("#0000FF", 0, 1));
        var device = Bind(DeviceKind.Light, 0, red, blue);
        var file = BinReader.ReadLght(Build(device).Bin!);
        Assert.Equal((new Rgb(255, 0, 0), new Rgb(0, 0, 255)), (file.At(0).Slot(0), file.At(0).Slot(1)));

        _history.Do(Edits.SetDeviceTimelines(_project, device, [blue.Id, red.Id]));
        file = BinReader.ReadLght(Build(device).Bin!);
        Assert.Equal((new Rgb(0, 0, 255), new Rgb(255, 0, 0)), (file.At(0).Slot(0), file.At(0).Slot(1)));
    }

    // ---- the device and its timelines

    [Fact]
    public void A_timeline_of_the_wrong_kind_for_the_device_is_refused_by_the_builder_too()
    {
        var light = Add(TimelineKind.Light, Colour("#FFFFFF", 0, 1));
        var result = new ShowBuilder(_project, Media).Build(DeviceKind.Poi, [light], ledCount: 4);

        Assert.False(result.Succeeded);
        Assert.Equal(BuildIssueCode.TimelineKindMismatch, Assert.Single(result.Errors).Code);
    }
}
