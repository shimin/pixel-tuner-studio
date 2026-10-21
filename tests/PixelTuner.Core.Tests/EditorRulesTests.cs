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
}
