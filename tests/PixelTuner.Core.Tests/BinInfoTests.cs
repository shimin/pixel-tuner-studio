using PixelTuner.Core.Editing;
using PixelTuner.Core.Export;
using PixelTuner.Core.Model;
using SkiaSharp;

namespace PixelTuner.Core.Tests;

/// <summary>The inspector's ".bin" line: where an item lands in the file of its device.</summary>
public class BinInfoTests
{
    private readonly ShowProject _project = new();
    private readonly UndoHistory _history = new();
    private readonly Dictionary<string, byte[]> _files = [];

    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    private ShowMedia Media => new(id => new MemoryStream(_files[id]));

    private TimelineItem Pic(string name, int width, double start, double length = 1, int height = 4, byte brightness = 100)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(new SKColor(200, 100, 50));
        using var image = SKImage.FromBitmap(bitmap);
        _files[name] = image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
        return new TimelineItem
        {
            Kind = ItemKind.Image, ResourceId = name, SourcePath = name, MediaWidth = width, MediaHeight = height,
            Start = S(start), Duration = S(length), Stretching = 4, Brightness = brightness,
        };
    }

    private Timeline Line(string kind, params TimelineItem[] items)
    {
        var timeline = new Timeline { Kind = kind };
        _history.Do(Edits.AddTimeline(_project, timeline));
        foreach (var item in items) _history.Do(Edits.AddItem(timeline, item));
        return timeline;
    }

    [Fact]
    public void An_item_knows_its_entry_among_the_pauses_its_block_and_who_shares_it()
    {
        var a1 = Pic("a", 6, 0);
        var b = Pic("b", 3, 2);
        var a2 = Pic("a", 6, 4);
        var timeline = Line(TimelineKind.Pixel, a1, b, a2);   // entries: a, pause, b, pause, a

        var first = BinInfo.Of(_project, Media, timeline, a1);
        var middle = BinInfo.Of(_project, Media, timeline, b);
        var last = BinInfo.Of(_project, Media, timeline, a2);

        Assert.Equal((1, 5), (first.EntryNumber, first.EntryCount));
        Assert.Equal((3, 5), (middle.EntryNumber, middle.EntryCount));
        Assert.Equal((5, 5), (last.EntryNumber, last.EntryCount));
        Assert.Equal(5 + 6 * 4 * 3, first.BlockSize);          // header, then 6 columns of 4 LEDs in R, G, B
        Assert.Equal(5 + 3 * 4 * 3, middle.BlockSize);
        Assert.Equal([a2.Id], first.SharedWithItems);
        Assert.Equal([a1.Id], last.SharedWithItems);
        Assert.Empty(middle.SharedWithItems);
        Assert.True(first.Built);
        Assert.Equal(BinReaderLength(timeline), first.FileSize);
    }

    private long BinReaderLength(Timeline timeline) =>
        new ShowBuilder(_project, Media).Build(BinInfo.DeviceFor(_project, timeline)).Bin!.Length;

    [Fact]
    public void Another_brightness_is_another_block_and_the_file_size_follows()
    {
        var a1 = Pic("a", 6, 0);
        var a2 = Pic("a", 6, 2);
        var timeline = Line(TimelineKind.Pixel, a1, a2);
        var shared = BinInfo.Of(_project, Media, timeline, a1);
        Assert.Equal([a2.Id], shared.SharedWithItems);

        _history.Do(Edits.SetBrightness(a2, 50));
        var apart = BinInfo.Of(_project, Media, timeline, a1);

        Assert.Empty(apart.SharedWithItems);
        Assert.Equal(shared.FileSize + 5 + 6 * 4 * 3, apart.FileSize);   // a block more
    }

    [Fact]
    public void The_device_that_plays_the_timeline_sets_the_height_and_the_memory()
    {
        var a = Pic("a", 6, 0, height: 4);
        var timeline = Line(TimelineKind.Pixel, a);
        var device = new DeviceBinding { DeviceType = DeviceKind.Hoop, LedCount = 4, MemoryBytes = 123_456, TimelineIds = [timeline.Id] };
        _history.Do(Edits.AddDevice(_project, device));

        var info = BinInfo.Of(_project, Media, timeline, a);

        Assert.Equal(123_456, info.MemoryBytes);
        Assert.Same(device, BinInfo.DeviceFor(_project, timeline));
    }

    [Fact]
    public void A_file_that_cannot_be_built_gives_the_reason_for_the_item()
    {
        var a = Pic("a", 6, 0);
        var timeline = Line(TimelineKind.Pixel, a);
        a.ResourceId = null;   // the file is missing

        var info = BinInfo.Of(_project, Media, timeline, a);

        Assert.False(info.Built);
        Assert.Null(info.EntryNumber);
        Assert.Equal(BuildIssueCode.MissingMedia, Assert.Single(info.Issues).Code);
    }

    [Fact]
    public void A_light_item_has_the_file_and_the_warnings_but_no_entry()
    {
        var odd = new TimelineItem { Kind = ItemKind.Solid, Color = "#FF0000", Start = TimeSpan.Zero, Duration = TimeSpan.FromMilliseconds(103) };
        var timeline = Line(TimelineKind.Light, odd);

        var info = BinInfo.Of(_project, Media, timeline, odd);

        Assert.True(info.Built);
        Assert.Null(info.EntryNumber);
        Assert.Equal(BuildIssueCode.LightStepNotMultiple, Assert.Single(info.Issues).Code);
    }

    [Fact]
    public void Videos_of_one_gif_at_other_offsets_share_a_block_and_say_so()
    {
        var gif = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "LegacyProjects", "glasses", "glasses.gif"));
        _files["gif"] = gif;
        TimelineItem Video(double start, int offset) => new()
        {
            Kind = ItemKind.Video, ResourceId = "gif", SourcePath = "g.gif", Start = S(start), Duration = S(1), Stretching = 40, OffsetX = offset,
        };
        var first = Video(0, 0);
        var second = Video(1, 3);
        var timeline = Line(TimelineKind.Video, first, second);
        var layout = new LedLayout { Columns = 2, Rows = 2, Leds = [0, 1, 2, 3] };
        _project.LedLayouts.Add(layout);
        _history.Do(Edits.SetLedLayout(_project, timeline, layout.Id));

        var info = BinInfo.Of(_project, Media, timeline, second);

        Assert.Equal([first.Id], info.SharedWithItems);
        Assert.Equal(BuildIssueCode.SharedBlockOffsets, Assert.Single(info.Issues).Code);   // the offset of the second is not played
        Assert.Equal((2, 2), (info.EntryNumber, info.EntryCount));
    }
}
