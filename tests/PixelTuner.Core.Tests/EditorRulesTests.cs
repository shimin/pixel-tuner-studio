using PixelTuner.Core.Editing;
using PixelTuner.Core.Export;
using PixelTuner.Core.Model;
using SkiaSharp;

namespace PixelTuner.Core.Tests;

/// <summary>
/// The rules table of the plan, one test per row: a change made the way the editor makes it (an
/// <see cref="Edits"/> factory through <see cref="UndoHistory"/>), the .bin built from the model, and the
/// field read back with <see cref="BinReader"/>. Undo must bring the old .bin back.
/// </summary>
public class EditorRulesTests
{
    private readonly ShowProject _project = new();
    private readonly UndoHistory _history = new();
    private readonly Dictionary<string, byte[]> _files = [];

    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    /// <summary>A picture file in memory, filled with one colour.</summary>
    private TimelineItem Picture(string name, int width, int height, double start, double length, SKColor? color = null)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color ?? new SKColor(200, 100, 50));
        using var image = SKImage.FromBitmap(bitmap);
        _files[name] = image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
        return new TimelineItem
        {
            Kind = ItemKind.Image, ResourceId = name, SourcePath = name, MediaWidth = width, MediaHeight = height,
            Start = S(start), Duration = S(length), Stretching = 4,
        };
    }

    private Timeline PixelTimeline(string name = "Poi")
    {
        var timeline = new Timeline { Name = name, Kind = TimelineKind.Pixel, Color = "#FF0000FF" };
        _history.Do(Edits.AddTimeline(_project, timeline));
        return timeline;
    }

    private ShowMedia Media => new(id => new MemoryStream(_files[id]));

    private byte[] PixelBin(ushort height) =>
        new PixelExport(Media).Export(_project.Timelines, height);

    [Fact]
    public void Height_of_the_timeline_is_H_of_the_file()
    {
        var timeline = PixelTimeline();
        _history.Do(Edits.AddItem(timeline, Picture("a", 8, 36, 0, 1)));

        Assert.Equal(36, BinReader.ReadPpoi(PixelBin(36)).Height);
        Assert.Equal(36, Edits.PixelHeight(timeline));

        // A device with another number of LEDs gets no timeline of this one.
        Assert.Empty(BinReader.ReadPpoi(PixelBin(80)).Timelines);
    }

    /// <summary>One edit: the file before and after. The edit is undone again, and undo must bring the first file back.</summary>
    private (PpoiFile Before, PpoiFile After) Changed(ushort height, Edit edit)
    {
        byte[] before = PixelBin(height);
        _history.Do(edit);
        byte[] after = PixelBin(height);
        _history.Undo();
        Assert.Equal(before, PixelBin(height));
        return (BinReader.ReadPpoi(before), BinReader.ReadPpoi(after));
    }

    private (Timeline Timeline, TimelineItem First, TimelineItem Second) TwoPictures()
    {
        var timeline = PixelTimeline();
        var first = Picture("a", 6, 4, 0, 2);
        var second = Picture("b", 3, 4, 3, 1, new SKColor(10, 20, 30));
        _history.Do(Edits.AddItem(timeline, first));
        _history.Do(Edits.AddItem(timeline, second));
        return (timeline, first, second);
    }

    [Fact]
    public void Position_and_length_set_entry_starts_and_the_pause_between()
    {
        var (timeline, first, second) = TwoPictures();
        var entries = BinReader.ReadPpoi(PixelBin(4)).Timelines[0].Entries;
        // 0-2 s picture, 2-3 s pause, 3-4 s picture: three entries, the middle one black.
        Assert.Equal([0u, 2000u, 3000u], entries.Select(e => e.StartMs));
        Assert.Equal(4000u, BinReader.ReadPpoi(PixelBin(4)).Timelines[0].DurationMs);

        var (_, after) = Changed(4, Edits.ResizeItem(timeline, first, S(3)));   // the pause is gone
        Assert.Equal([0u, 3000u], after.Timelines[0].Entries.Select(e => e.StartMs));

        (_, after) = Changed(4, Edits.MoveItem(timeline, second, S(5)));
        Assert.Equal([0u, 2000u, 5000u], after.Timelines[0].Entries.Select(e => e.StartMs));
        Assert.Equal(6000u, after.Timelines[0].DurationMs);

        (_, after) = Changed(4, Edits.TrimItemStart(timeline, second, S(2)));   // starts earlier, ends where it did
        Assert.Equal([0u, 2000u], after.Timelines[0].Entries.Select(e => e.StartMs));
        Assert.Equal(4000u, after.Timelines[0].DurationMs);
    }

    [Fact]
    public void Stretching_and_laser_are_fields_of_the_entry()
    {
        var (_, first, _) = TwoPictures();

        var (before, after) = Changed(4, Edits.SetStretching(first, 9));
        Assert.Equal(4, before.Timelines[0].Entries[0].Stretching);
        Assert.Equal(9, after.Timelines[0].Entries[0].Stretching);

        (before, after) = Changed(4, Edits.SetLaser(first, true));
        Assert.Equal<ushort>(0, before.Timelines[0].Entries[0].Mask);
        Assert.Equal<ushort>(0xFFFF, after.Timelines[0].Entries[0].Mask);   // pixel devices write all bits
    }

    [Fact]
    public void Timeline_colour_is_written_divided_by_ten()
    {
        var (timeline, _, _) = TwoPictures();

        var (_, after) = Changed(4, Edits.SetTimelineColor(timeline, "#FFFA3C28"));
        Assert.Equal(new PixelTuner.Core.Imaging.Rgb(25, 6, 4), after.Timelines[0].Color);
        Assert.Equal(new PixelTuner.Core.Imaging.Rgb(70, 0, 0), after.Timelines[0].SelectionColor);
    }

    [Fact]
    public void Brightness_changes_the_pixels_and_a_new_brightness_is_a_new_block()
    {
        var timeline = PixelTimeline();
        var one = Picture("a", 4, 4, 0, 1);
        var two = Picture("a", 4, 4, 1, 1);   // the same file
        _history.Do(Edits.AddItem(timeline, one));
        _history.Do(Edits.AddItem(timeline, two));

        var shared = BinReader.ReadPpoi(PixelBin(4));
        Assert.Single(BinReader.Images(shared));

        var (_, after) = Changed(4, Edits.SetBrightness(two, 50));
        Assert.Equal(2, BinReader.Images(after).Count());
        var entries = after.Timelines[0].Entries;
        Assert.Equal(200, entries[0].Image.Value(0, 0, 0));    // red, full brightness: 200 stays 200
        Assert.Equal(100, entries[1].Image.Value(0, 0, 0));    // half
        Assert.True(after.Size > shared.Size);
    }

    [Fact]
    public void Flip_turns_the_columns_over_in_the_block()
    {
        var timeline = PixelTimeline();
        using (var bitmap = new SKBitmap(1, 3))
        {
            bitmap.SetPixel(0, 0, new SKColor(255, 0, 0));
            bitmap.SetPixel(0, 1, new SKColor(0, 255, 0));
            bitmap.SetPixel(0, 2, new SKColor(0, 0, 255));
            using var image = SKImage.FromBitmap(bitmap);
            _files["abc"] = image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
        }
        _history.Do(Edits.AddItem(timeline, new TimelineItem { Kind = ItemKind.Image, ResourceId = "abc", SourcePath = "abc", MediaWidth = 1, MediaHeight = 3, Duration = S(1) }));

        var (before, after) = Changed(3, Edits.SetFlipVertical(timeline, true));
        Assert.Equal(255, before.Timelines[0].Entries[0].Image.Value(0, 0, 0));   // red on top
        Assert.Equal(255, after.Timelines[0].Entries[0].Image.Value(0, 2, 0));    // red at the bottom
    }

    private TimelineItem Colour(string color, double start, double length) =>
        new() { Kind = ItemKind.Solid, Color = color, Start = S(start), Duration = S(length) };

    private LghtFile Light(int rate = 5) =>
        BinReader.ReadLght(LightExport.Export(_project.Timelines, rate));

    [Fact]
    public void Light_colours_are_the_samples_of_the_channel()
    {
        var timeline = new Timeline { Kind = TimelineKind.Light };
        _history.Do(Edits.AddTimeline(_project, timeline));
        var colour = Colour("#102030", 0, 1);
        _history.Do(Edits.AddItem(timeline, colour));

        Assert.Equal(new PixelTuner.Core.Imaging.Rgb(0x10, 0x20, 0x30), Light().At(500).Slot(0));
        _history.Do(Edits.SetItemColor(colour, "#FF0000"));
        Assert.Equal(new PixelTuner.Core.Imaging.Rgb(255, 0, 0), Light().At(500).Slot(0));
        _history.Undo();
        Assert.Equal(new PixelTuner.Core.Imaging.Rgb(0x10, 0x20, 0x30), Light().At(500).Slot(0));
    }

    [Fact]
    public void Three_monochrome_channels_share_one_slot()
    {
        for (int i = 0; i < 3; i++)
        {
            var timeline = new Timeline { Kind = TimelineKind.Light };
            _history.Do(Edits.AddTimeline(_project, timeline));
            _history.Do(Edits.AddItem(timeline, Colour($"#{(i + 1) * 0x10:X2}0000", 0, 1)));
        }
        Assert.Equal(3, Light().Slots);

        foreach (var timeline in _project.Timelines) _history.Do(Edits.SetMonochrome(timeline, true));
        var file = Light();
        Assert.Equal(1, file.Slots);
        Assert.Equal(new PixelTuner.Core.Imaging.Rgb(0x10, 0x20, 0x30), file.At(0).Slot(0));   // the red values of the three channels
    }

    [Fact]
    public void Effect_times_change_the_samples()
    {
        var timeline = new Timeline { Kind = TimelineKind.Light };
        _history.Do(Edits.AddTimeline(_project, timeline));
        var effect = new TimelineItem
        {
            Kind = ItemKind.Effect, Effect = LightEffectKind.SquareWave, Color = "#000000", ColorLast = "#FFFFFF",
            T1 = 100, T2 = 100, Start = TimeSpan.Zero, Duration = S(1),
        };
        _history.Do(Edits.AddItem(timeline, effect));
        Assert.Equal(0, Light().At(50).Slot(0).R);      // first half of the period: the first colour
        Assert.Equal(255, Light().At(150).Slot(0).R);   // second half: the second

        _history.Do(Edits.SetEffectTimes(effect, 300, 100));
        Assert.Equal(0, Light().At(150).Slot(0).R);     // the first colour lasts 300 ms now
        _history.Do(Edits.SetEffect(effect, LightEffectKind.LinearTransition));
        Assert.InRange(Light().At(500).Slot(0).R, 120, 135);
    }
}
