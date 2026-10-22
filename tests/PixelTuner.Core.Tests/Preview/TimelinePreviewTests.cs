using PixelTuner.Core.Editing;
using PixelTuner.Core.Export;
using PixelTuner.Core.Imaging;
using PixelTuner.Core.Model;
using PixelTuner.Core.Preview;
using SkiaSharp;

namespace PixelTuner.Core.Tests.Preview;

/// <summary>
/// The preview plays what the device gets. The gate: a pixel of the preview is the byte of the block in the .bin, and the column that is lit
/// at a time is the one the device holds then, at the speed of its kind of device.
/// </summary>
public class TimelinePreviewTests
{
    private readonly ShowProject _project = new();
    private readonly UndoHistory _history = new();
    private readonly Dictionary<string, byte[]> _files = [];

    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);
    private static TimeSpan Ms(double milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    private ShowMedia Media => new(id => new MemoryStream(_files[id]));

    // A picture whose every pixel has its own colour, so that a wrong column or row shows.
    private TimelineItem Picture(string name, int width, int height, double start, double length, int stretching = 10, byte brightness = 100)
    {
        using var bitmap = new SKBitmap(width, height);
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                bitmap.SetPixel(x, y, new SKColor((byte)(10 + x * 30), (byte)(20 + y * 50), (byte)(30 + (x + y) * 7)));
        using var image = SKImage.FromBitmap(bitmap);
        _files[name] = image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
        return new TimelineItem
        {
            Kind = ItemKind.Image, ResourceId = name, SourcePath = name, MediaWidth = width, MediaHeight = height,
            Start = S(start), Duration = S(length), Stretching = stretching, Brightness = brightness,
        };
    }

    private Timeline Add(string kind, params TimelineItem[] items)
    {
        var timeline = new Timeline { Kind = kind, Name = kind };
        _history.Do(Edits.AddTimeline(_project, timeline));
        foreach (var item in items) _history.Do(Edits.AddItem(timeline, item));
        return timeline;
    }

    private (TimelinePreview Preview, byte[] Bin) Play(string deviceKind, Timeline timeline, int leds = 0)
    {
        var device = new DeviceBinding { DeviceType = deviceKind, LedCount = leds, TimelineIds = [timeline.Id] };
        _project.Devices.Add(device);
        var result = new ShowBuilder(_project, Media).Build(device);
        Assert.True(result.Succeeded, string.Join(", ", result.Issues.Select(i => i.Code)));
        return (TimelinePreview.FromBin(result.Bin!, deviceKind)!, result.Bin!);
    }

    // The byte of a channel in the block, worked out from the layout of the file (docs/binary-format.md) and not through BinReader.Value.
    private static byte Byte(PpoiImage block, int column, int row, int channel) =>
        block.Pixels.Span[(column * 3 + channel) * block.Height + row];

    // ---- pixel: pixel equals byte

    [Fact]
    public void Every_pixel_of_the_preview_is_the_byte_of_the_block_in_the_file_for_every_column_of_the_picture()
    {
        var timeline = Add(TimelineKind.Pixel, Picture("a", 6, 4, 1, 1, stretching: 10));
        var (preview, bin) = Play(DeviceKind.Poi, timeline, 4);
        var file = BinReader.ReadPpoi(bin);
        var entry = file.Timelines[0].Entries.Single(e => e.StartMs == 1000);
        Assert.Equal(8, entry.Image.BitsPerChannel);

        for (int column = 0; column < 6; column++)
        {
            // The middle of the time the device holds the column: 10 x 50 µs = 0.5 ms each.
            var frame = preview.At(S(1) + Ms(0.5 * column + 0.25));
            Assert.Equal(column, frame.Column);
            for (int row = 0; row < 4; row++)
                Assert.Equal(new Rgb(Byte(entry.Image, column, row, 0), Byte(entry.Image, column, row, 1), Byte(entry.Image, column, row, 2)), frame.Colors[row]);
        }
    }

    [Fact]
    public void Brightness_is_already_in_the_block_so_the_preview_shows_the_dimmed_picture()
    {
        var timeline = Add(TimelineKind.Pixel, Picture("a", 3, 4, 0, 1, brightness: 50));
        var (preview, bin) = Play(DeviceKind.Poi, timeline, 4);
        var entry = BinReader.ReadPpoi(bin).Timelines[0].Entries.Single(e => e.StartMs == 0);

        var frame = preview.At(Ms(0.1));
        Assert.Equal(Byte(entry.Image, 0, 2, 1), frame.Colors[2].G);

        var bright = Add(TimelineKind.Pixel, Picture("b", 3, 4, 0, 1, brightness: 100));
        var (full, _) = Play(DeviceKind.Poi, bright, 4);
        Assert.True(frame.Colors[2].G < full.At(Ms(0.1)).Colors[2].G);
    }

    [Fact]
    public void A_picture_plays_again_from_its_first_column_until_the_end_of_the_item()
    {
        var timeline = Add(TimelineKind.Pixel, Picture("a", 4, 3, 0, 1, stretching: 20));   // 1 ms a column, 4 ms a pass
        var (preview, _) = Play(DeviceKind.Poi, timeline, 3);

        Assert.Equal([0, 1, 2, 3, 0, 1, 2, 3, 0], Enumerable.Range(0, 9).Select(i => preview.At(Ms(i + 0.5)).Column));
        Assert.Equal(Ms(1), preview.At(Ms(2)).ColumnTime);
        Assert.Equal(Ms(4), preview.At(Ms(2)).PassTime);
        Assert.Equal(3, preview.At(Ms(2)).Colors.Count);
    }

    [Fact]
    public void The_pauses_before_between_and_after_items_are_dark_and_after_the_end_nothing_plays()
    {
        var timeline = Add(TimelineKind.Pixel, Picture("a", 2, 3, 1, 1), Picture("b", 2, 3, 3, 1));
        var (preview, _) = Play(DeviceKind.Poi, timeline, 3);

        var before = preview.At(S(0.5));
        Assert.True(before.IsPlaying);
        Assert.All(before.Colors, c => Assert.Equal(Rgb.Black, c));
        Assert.All(preview.At(S(2.5)).Colors, c => Assert.Equal(Rgb.Black, c));
        Assert.Contains(preview.At(S(1.5)).Colors, c => c != Rgb.Black);
        Assert.Contains(preview.At(S(3.5)).Colors, c => c != Rgb.Black);

        var after = preview.At(S(5));
        Assert.False(after.IsPlaying);
        Assert.Equal(-1, after.Entry);
        Assert.All(after.Colors, c => Assert.Equal(Rgb.Black, c));
        Assert.Equal(S(4), preview.Duration);
    }

    [Fact]
    public void The_entry_of_a_time_is_the_last_one_that_has_started_and_the_picture_comes_with_it()
    {
        var timeline = Add(TimelineKind.Pixel, Picture("a", 2, 3, 0, 1), Picture("b", 5, 3, 1, 1));
        var (preview, _) = Play(DeviceKind.Poi, timeline, 3);

        var first = preview.At(S(0.5));
        var second = preview.At(S(1.5));
        Assert.True(second.Entry > first.Entry);
        Assert.Equal(2, first.Picture!.Width);
        Assert.Equal(5, second.Picture!.Width);
        Assert.Equal(3, second.Picture.Height);
        Assert.Equal(second.Picture.Column(0), preview.At(S(1) + Ms(0.01)).Colors);
    }

    [Fact]
    public void The_laser_of_an_item_reaches_the_frame()
    {
        var item = Picture("a", 2, 3, 0, 1);
        var timeline = Add(TimelineKind.Pixel, item);
        _history.Do(Edits.SetLaser(item, true));
        var (preview, _) = Play(DeviceKind.Poi, timeline, 3);
        Assert.True(preview.At(S(0.5)).Laser);
    }

    // ---- the speed of each kind of device

    [Fact]
    public void A_hoop_plays_at_the_speed_of_a_poi_and_a_fan_holds_a_column_for_a_millisecond_a_step()
    {
        var timeline = Add(TimelineKind.Pixel, Picture("a", 4, 3, 0, 1, stretching: 40));

        var (hoop, _) = Play(DeviceKind.Hoop, timeline, 3);
        Assert.Equal(TimeSpan.FromTicks(40 * 500), hoop.At(S(0.5)).ColumnTime);        // 2 ms

        var (fan, _) = Play(DeviceKind.Fan, timeline, 3);
        Assert.Equal(Ms(40), fan.At(S(0.5)).ColumnTime);
        Assert.Equal([0, 0, 1, 1, 2, 3, 0], new[] { 0.0, 39.0, 41.0, 79.0, 81.0, 130.0, 170.0 }.Select(t => fan.At(Ms(t)).Column));
    }

    [Fact]
    public void An_epic_poi_holds_a_colour_for_k_over_five_milliseconds()
    {
        var item = Picture("g", 5, 1, 0, 1, stretching: 10);
        item.Kind = ItemKind.Gradient;
        var timeline = Add(TimelineKind.EpicPoi, item);
        var (preview, bin) = Play(DeviceKind.EpicPoi, timeline);

        Assert.Equal(Ms(2), preview.At(S(0.5)).ColumnTime);                        // k = 10: 2 ms
        var entry = BinReader.ReadPpoi(bin).Timelines[0].Entries.Single(e => e.StartMs == 0);
        Assert.Equal(new Rgb(Byte(entry.Image, 1, 0, 0), Byte(entry.Image, 1, 0, 1), Byte(entry.Image, 1, 0, 2)), preview.At(Ms(2.5)).Colors[0]);
        Assert.Equal(1, preview.At(Ms(2.5)).Column);
    }

    // ---- light

    [Fact]
    public void Light_shows_the_colours_the_samples_of_the_file_hold()
    {
        var timeline = Add(TimelineKind.Light,
            new TimelineItem { Kind = ItemKind.Solid, Color = "#FF2040A0", Start = S(0), Duration = S(1) },
            new TimelineItem { Kind = ItemKind.Solid, Color = "#FFC00000", Start = S(1), Duration = S(1) });
        var (preview, bin) = Play(DeviceKind.Light, timeline);
        var file = BinReader.ReadLght(bin);

        Assert.True(preview.IsLight);
        Assert.Equal(file.Slots, preview.Height);
        foreach (double seconds in new[] { 0.0, 0.5, 0.99, 1.0, 1.5 })
        {
            var sample = file.At((uint)(seconds * 1000));
            var frame = preview.At(S(seconds));
            for (int slot = 0; slot < file.Slots; slot++) Assert.Equal(sample.Slot(slot), frame.Colors[slot]);
        }
        Assert.Contains(new Rgb(0x20, 0x40, 0xA0), preview.At(S(0.5)).Colors);
        Assert.Contains(new Rgb(0xC0, 0, 0), preview.At(S(1.5)).Colors);
        Assert.False(preview.At(S(10)).IsPlaying);
    }

    // ---- video

    private static string Gif() => Path.Combine(AppContext.BaseDirectory, "Fixtures", "LegacyProjects", "glasses", "glasses.gif");

    [Fact]
    public void A_video_module_shows_a_frame_for_k_milliseconds_with_one_colour_per_led_of_the_layout()
    {
        _files["gif"] = File.ReadAllBytes(Gif());
        var layout = new LedLayout { Name = "prop", Columns = 3, Rows = 2, Leds = [0, 1, 2, 5, 4, 3] };
        _project.LedLayouts.Add(layout);
        var item = new TimelineItem { Kind = ItemKind.Video, ResourceId = "gif", SourcePath = "glasses.gif", Start = S(0), Duration = S(2), Stretching = 40 };
        var timeline = Add(TimelineKind.Video, item);
        _history.Do(Edits.SetLedLayout(_project, timeline, layout.Id));
        var (preview, bin) = Play(DeviceKind.Video, timeline);
        var entry = BinReader.ReadPpoi(bin).Timelines[0].Entries.Single(e => e.StartMs == 0);

        Assert.Equal(6, preview.Height);
        Assert.Equal(Ms(40), preview.At(Ms(10)).ColumnTime);
        for (int frameNumber = 0; frameNumber < 5; frameNumber++)
        {
            var frame = preview.At(Ms(40 * frameNumber + 20));
            Assert.Equal(frameNumber % entry.Image.Width, frame.Column);
            for (int led = 0; led < 6; led++)
                Assert.Equal(new Rgb(Byte(entry.Image, frame.Column, led, 0), Byte(entry.Image, frame.Column, led, 1), Byte(entry.Image, frame.Column, led, 2)), frame.Colors[led]);
        }
        Assert.Equal(6, TimelinePreview.Positions(layout).Count);
        Assert.Equal((2, 1), TimelinePreview.Positions(layout)[3]);   // LED 4 is the cell 5: column 2, row 1
    }

    // ---- building it from a project

    [Fact]
    public void A_timeline_that_cannot_be_built_has_no_preview_and_the_issues_say_why()
    {
        var timeline = Add(TimelineKind.Video, new TimelineItem { Kind = ItemKind.Video, ResourceId = "gif", Start = S(0), Duration = S(1), Stretching = 40 });
        _files["gif"] = File.ReadAllBytes(Gif());
        var preview = TimelinePreview.Build(_project, Media, timeline, out var issues);
        Assert.Null(preview);
        Assert.Contains(issues, i => i.Code == BuildIssueCode.NoLedLayout);
    }

    [Fact]
    public void A_timeline_is_built_for_the_device_that_plays_it_or_one_made_up_for_its_kind()
    {
        var timeline = Add(TimelineKind.Pixel, Picture("a", 3, 4, 0, 1));
        var preview = TimelinePreview.Build(_project, Media, timeline, out var issues);
        Assert.NotNull(preview);
        Assert.Equal(DeviceKind.Poi, preview.DeviceType);
        Assert.Equal(4, preview.Height);
        Assert.DoesNotContain(issues, i => i.Severity == BuildSeverity.Error);
    }

    [Fact]
    public void Something_that_is_not_a_show_file_has_no_preview()
    {
        Assert.Null(TimelinePreview.FromBin([1, 2, 3], DeviceKind.Poi));
        Assert.Null(TimelinePreview.FromBin([], DeviceKind.Poi));
    }
}
