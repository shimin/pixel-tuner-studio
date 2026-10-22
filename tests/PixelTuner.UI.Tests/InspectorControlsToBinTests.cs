using PixelTuner.Core.Editing;
using PixelTuner.Core.Export;
using PixelTuner.Core.Imaging;
using PixelTuner.Core.Model;
using PixelTuner.Package;
using PixelTuner.UI.ViewModels;
using SkiaSharp;

namespace PixelTuner.UI.Tests;

/// <summary>
/// The gate of the inspector epic: every control writes the field of the .bin the plan says it does. Each test turns a control of the
/// inspector, builds the file of the device that plays the timeline, reads the field back, and checks that undo brings the old file back.
/// </summary>
public sealed class InspectorControlsToBinTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pt-controls").FullName;
    private readonly ProjectDocument _doc = ProjectDocument.CreateNew("Show");
    private readonly FakePicker _picker = new();
    private readonly EditorViewModel _editor;

    public InspectorControlsToBinTests() => _editor = new EditorViewModel(_doc, null, () => _picker);

    public void Dispose()
    {
        _editor.Dispose();
        Directory.Delete(_dir, true);
    }

    private static string Gif() => Path.Combine(AppContext.BaseDirectory, "Fixtures", "glasses", "glasses.gif");

    private string Picture(string name, int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        bitmap.SetPixel(0, 0, new SKColor(255, 0, 0));   // the top left pixel is red, so that turning the picture over shows
        using var image = SKImage.FromBitmap(bitmap);
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, image.Encode(SKEncodedImageFormat.Png, 100).ToArray());
        return path;
    }

    private ShowBuilder Builder() => new(_doc.Project, new ShowMedia(id => _doc.OpenMedia(id)!));

    private byte[] Bin(string deviceKind, Timeline timeline, int leds = 0) =>
        Builder().Build(deviceKind, [timeline], leds).Bin ?? throw new InvalidOperationException("not built");

    /// <summary>Turns the control, returns the file before and after, and checks that one undo gives the first file back.</summary>
    private (byte[] Before, byte[] After) Turn(Func<byte[]> build, Action control)
    {
        byte[] before = build();
        control();
        byte[] after = build();
        _doc.Undo();
        Assert.Equal(before, build());
        _doc.Redo();
        Assert.Equal(after, build());
        return (before, after);
    }

    // ---- pixel timeline, 4 LEDs

    private async Task<(Timeline Timeline, TimelineItem First, TimelineItem Second)> Pixel()
    {
        _editor.AddPixelTimelineCommand.Execute(null);
        var timeline = _doc.Project.Timelines[^1];
        _picker.ImagePath = Picture("a.png", 6, 4, new SKColor(200, 100, 50));
        await _editor.AddItemCommand.ExecuteAsync(null);
        _editor.LengthText = "2";
        _editor.Timelines.SetPlayhead(TimeSpan.FromSeconds(3));
        _picker.ImagePath = Picture("b.png", 3, 4, new SKColor(10, 20, 30));
        await _editor.AddItemCommand.ExecuteAsync(null);
        _editor.LengthText = "1";
        return (timeline, timeline.Items[0], timeline.Items[1]);
    }

    private byte[] PoiBin(Timeline t) => Bin(DeviceKind.Poi, t, 4);

    private static PpoiTimeline Line(byte[] bin) => BinReader.ReadPpoi(bin).Timelines[0];

    [Fact]
    public async Task Start_and_length_set_the_starts_of_the_entries_and_the_pause_between()
    {
        var (timeline, first, second) = await Pixel();   // picture 0-2 s, pause 2-3 s, picture 3-4 s
        Assert.Equal([0u, 2000u, 3000u], Line(PoiBin(timeline)).Entries.Select(e => e.StartMs));

        _editor.Timelines.Select(second);
        var (_, after) = Turn(() => PoiBin(timeline), () => _editor.StartText = "5");
        Assert.Equal([0u, 2000u, 5000u], Line(after).Entries.Select(e => e.StartMs));
        Assert.Equal(6000u, Line(after).DurationMs);

        _editor.Timelines.Select(first);
        (_, after) = Turn(() => PoiBin(timeline), () => _editor.LengthText = "3");   // the pause is gone
        Assert.Equal([0u, 3000u, 5000u], Line(after).Entries.Select(e => e.StartMs));   // the second picture is at 5 s now: the pause before it stays
    }

    [Fact]
    public async Task Stretching_brightness_and_laser_write_the_entry_and_the_pixels()
    {
        var (timeline, first, _) = await Pixel();
        _editor.Timelines.Select(first);

        var (before, after) = Turn(() => PoiBin(timeline), () => _editor.Stretching = 9);
        Assert.Equal((4, 9), ((int)Line(before).Entries[0].Stretching, (int)Line(after).Entries[0].Stretching));

        (before, after) = Turn(() => PoiBin(timeline), () => _editor.LaserOn = true);
        Assert.Equal((false, true), (Line(before).Entries[0].LaserOn, Line(after).Entries[0].LaserOn));

        (before, after) = Turn(() => PoiBin(timeline), () => _editor.Brightness = 50);
        Assert.Equal((255, 127), (Line(before).Entries[0].Image.Value(0, 0, 0), Line(after).Entries[0].Image.Value(0, 0, 0)));   // red at full and at half brightness
    }

    [Fact]
    public async Task The_timeline_header_writes_colour_height_and_pixels()
    {
        var (timeline, _, _) = await Pixel();
        _editor.Timelines.Select(timeline);

        var (_, after) = Turn(() => PoiBin(timeline), () => _editor.TimelineColorText = "#C8641E");
        Assert.Equal(new Rgb(20, 10, 3), Line(after).Color);                 // each channel divided by ten

        var (flipBefore, flipAfter) = Turn(() => PoiBin(timeline), () => _editor.FlipVertical = true);
        Assert.Equal((255, 200), (Line(flipBefore).Entries[0].Image.Value(0, 0, 0), Line(flipBefore).Entries[0].Image.Value(0, 3, 0)));
        Assert.Equal(255, Line(flipAfter).Entries[0].Image.Value(0, 3, 0));   // red moved from the top LED to the bottom one

        // The height the header sets is H of the file, and a device of another height gets nothing of this timeline.
        Turn(() => PoiBin(timeline), () => _editor.TimelineHeight = 4);
        Assert.Equal(4, BinReader.ReadPpoi(PoiBin(timeline)).Height);
        Assert.False(Builder().Build(DeviceKind.Poi, [timeline], 36).Succeeded);
    }

    // ---- video timeline

    private async Task<(Timeline Timeline, TimelineItem Item)> Video()
    {
        _editor.AddVideoTimelineCommand.Execute(null);
        var timeline = _doc.Project.Timelines[^1];
        var layout = new LedLayout { Name = "prop", Columns = 4, Rows = 4, Leds = [0, 1, 2, 5, 6, 10] };
        var small = new LedLayout { Name = "small", Columns = 4, Rows = 4, Leds = [0, 1] };
        _doc.Project.LedLayouts.AddRange([layout, small]);
        _picker.GifPath = Gif();
        _editor.Timelines.Select(timeline);
        await _editor.AddItemCommand.ExecuteAsync(null);
        _editor.Timelines.Select(timeline);
        _editor.LayoutIndex = 1;   // "prop"; 0 is "(none)"
        return (timeline, timeline.Items[0]);
    }

    private byte[] VideoBin(Timeline t) => Bin(DeviceKind.Video, t);

    [Fact]
    public async Task The_video_inspector_writes_the_frame_time_offset_laser_and_the_layout()
    {
        var (timeline, item) = await Video();
        _editor.Timelines.Select(item);
        Assert.Equal(6, BinReader.ReadPpoi(VideoBin(timeline)).Height);   // one row per LED of the layout

        var (before, after) = Turn(() => VideoBin(timeline), () => _editor.Stretching = 100);
        Assert.Equal((40, 100), ((int)Line(before).Entries[0].Stretching, (int)Line(after).Entries[0].Stretching));   // milliseconds per frame

        (before, after) = Turn(() => VideoBin(timeline), () => _editor.OffsetX = 40);
        Assert.NotEqual(before, after);                                      // other pixels of the frame under the LEDs
        Assert.Equal(before.Length, after.Length);

        _editor.Timelines.Select(timeline);
        (before, after) = Turn(() => VideoBin(timeline), () => _editor.LayoutIndex = 2);
        Assert.Equal((6, 2), (BinReader.ReadPpoi(before).Height, BinReader.ReadPpoi(after).Height));   // H follows the layout
    }

    // ---- light timeline

    private LghtFile LightFile(Timeline t) => BinReader.ReadLght(Bin(DeviceKind.Light, t));

    [Fact]
    public void The_light_inspector_writes_colours_effect_times_and_the_monochrome_slot()
    {
        _editor.AddLightTimelineCommand.Execute(null);
        var timeline = _doc.Project.Timelines[^1];
        _editor.AddEffectCommand.Execute(null);   // black to white, linear, 5 s
        var effect = timeline.Items[0];
        Assert.Equal(0, LightFile(timeline).At(0).Slot(0).R);

        var (_, after) = Turn(() => Bin(DeviceKind.Light, timeline), () => _editor.ItemColor = "#FF0000");
        Assert.Equal(255, BinReader.ReadLght(after).At(0).Slot(0).R);          // the first colour

        var (before, changed) = Turn(() => Bin(DeviceKind.Light, timeline), () => _editor.ItemColorTo = "#0000FF");
        Assert.Equal(255, BinReader.ReadLght(changed).At(4990).Slot(0).B > 250 ? 255 : 0);   // the colour it goes to, at the end

        (_, after) = Turn(() => Bin(DeviceKind.Light, timeline), () => _editor.EffectIndex = 2);   // square wave
        Assert.NotEqual(before, after);

        _editor.T1 = 500;   // the length of the first half of a period
        var square = LightFile(timeline);
        Assert.Equal(square.At(100).Slot(0), BinReader.ReadLght(Bin(DeviceKind.Light, timeline)).At(100).Slot(0));
        (before, after) = Turn(() => Bin(DeviceKind.Light, timeline), () => _editor.T2 = 900);
        Assert.NotEqual(before, after);

        // Monochrome: three channels share one slot of the file.
        _editor.AddLightTimelineCommand.Execute(null);
        var second = _doc.Project.Timelines[^1];
        _editor.AddColorCommand.Execute(null);
        var two = Builder().Build(DeviceKind.Light, [timeline, second]).Bin!;
        Assert.Equal(2, BinReader.ReadLght(two).Slots);
        _editor.Timelines.Select(timeline);
        _editor.Monochrome = true;
        _editor.Timelines.Select(second);
        _editor.Monochrome = true;
        Assert.Equal(1, BinReader.ReadLght(Builder().Build(DeviceKind.Light, [timeline, second]).Bin!).Slots);
    }

    [Fact]
    public void The_light_colour_of_a_solid_item_is_the_sample()
    {
        _editor.AddLightTimelineCommand.Execute(null);
        var timeline = _doc.Project.Timelines[^1];
        _editor.AddColorCommand.Execute(null);

        Turn(() => Bin(DeviceKind.Light, timeline), () => _editor.ItemColor = "#336699");

        Assert.Equal(new Rgb(0x33, 0x66, 0x99), LightFile(timeline).At(100).Slot(0));
    }
}
