using PixelTuner.Core.Export;
using PixelTuner.Core.Imaging;
using PixelTuner.Core.Model;
using PixelTuner.Package;
using PixelTuner.UI.Timelines;
using PixelTuner.UI.ViewModels;
using SkiaSharp;

namespace PixelTuner.UI.Tests;

/// <summary>
/// The gate of the timelines epic: a pixel timeline built with commands, the inspector and the mouse from nothing,
/// and the .bin that comes out of it says what was done on the screen.
/// </summary>
public sealed class PixelTimelineFromScratchTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pt-scratch").FullName;
    private readonly ProjectDocument _doc = ProjectDocument.CreateNew("Show");
    private readonly FakePicker _picker = new();
    private readonly EditorViewModel _editor;

    public PixelTimelineFromScratchTests()
    {
        _editor = new EditorViewModel(_doc, null, () => _picker);
        _editor.Timelines.ViewWidth = 1170;   // 100 px per second
    }

    public void Dispose()
    {
        _editor.Dispose();
        Directory.Delete(_dir, true);
    }

    private string Picture(string name, int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, image.Encode(SKEncodedImageFormat.Png, 100).ToArray());
        return path;
    }

    private double X(double seconds) => _editor.Timelines.TimeToX(TimeSpan.FromSeconds(seconds));
    private static double Row(int row) => TimelineEditor.TracksTop + row * TimelineEditor.TrackHeight + 10;

    private void Drag(double fromX, double toX, int row = 0)
    {
        _editor.Timelines.PointerDown(fromX, Row(row));
        _editor.Timelines.PointerMove(toX, Row(row));
        _editor.Timelines.PointerUp(toX, Row(row));
    }

    [Fact]
    public async Task A_pixel_timeline_built_by_hand_becomes_the_bin_the_screen_describes()
    {
        // A timeline, coloured and flipped from the inspector.
        _editor.AddPixelTimelineCommand.Execute(null);
        var timeline = _doc.Project.Timelines[0];
        _editor.TimelineColorText = "#C8641E";
        _editor.FlipVertical = true;

        // First picture: 5 s long where the playhead is; its right edge is pulled back to 2 s.
        _picker.ImagePath = Picture("red.png", 6, 4, new SKColor(255, 0, 0));
        await _editor.AddItemCommand.ExecuteAsync(null);
        var first = timeline.Items[0];
        Assert.Equal(TimeSpan.FromSeconds(5), first.Duration);
        Drag(X(5) - 1, X(2) - 1);
        Assert.Equal(TimeSpan.FromSeconds(2), first.End);

        // Second picture, added at 3 s, with its own speed and the laser.
        _editor.Timelines.SetPlayhead(TimeSpan.FromSeconds(3));
        _picker.ImagePath = Picture("blue.png", 3, 4, new SKColor(0, 0, 255));
        await _editor.AddItemCommand.ExecuteAsync(null);
        var second = timeline.Items[1];
        Assert.Equal(TimeSpan.FromSeconds(3), second.Start);
        _editor.Stretching = 9;
        _editor.LaserOn = true;
        _editor.Brightness = 50;

        // Moved by the mouse to 4 s: the gap before it grows.
        Drag(X(5), X(6));
        Assert.Equal(TimeSpan.FromSeconds(4), second.Start);

        // A device with 4 LEDs plays the timeline.
        var device = new DeviceBinding { Name = "Poi", DeviceType = DeviceKind.Poi, LedCount = 4 };
        _doc.Execute(PixelTuner.Core.Editing.Edits.AddDevice(_doc.Project, device));
        _doc.Execute(PixelTuner.Core.Editing.Edits.SetDeviceTimelines(_doc.Project, device, [timeline.Id]));

        var result = new ShowBuilder(_doc.Project, new ShowMedia(id => _doc.OpenMedia(id)!)).Build(device);

        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Code)));
        var file = BinReader.ReadPpoi(result.Bin!);
        Assert.Equal(4, file.Height);
        var line = Assert.Single(file.Timelines);
        Assert.Equal(new Rgb(20, 10, 3), line.Color);   // 200, 100, 30 divided by ten

        // 0-2 s the red picture, 2-4 s a pause, 4-9 s the blue picture (5 s long, added at 3 s and moved to 4 s).
        Assert.Equal(9000u, line.DurationMs);
        Assert.Equal([0u, 2000u, 4000u], line.Entries.Select(e => e.StartMs));
        Assert.Equal([(ushort)4, (ushort)4, (ushort)9], line.Entries.Select(e => e.Stretching));
        Assert.Equal([false, false, true], line.Entries.Select(e => e.LaserOn));
        Assert.Equal(6, line.Entries[0].Image.Width);
        Assert.Equal(3, line.Entries[2].Image.Width);
        Assert.Equal(255, line.Entries[0].Image.Value(0, 0, 0));    // red stays red at full brightness on a 4-LED poi
        Assert.Equal(0, line.Entries[2].Image.Value(0, 0, 0));
        Assert.Equal(Convert.ToInt32(255 * 0.8 * 0.5), line.Entries[2].Image.Value(0, 0, 2));   // blue x 0.8 x brightness 50 %

        // Undo takes the whole show apart again, step by step, down to the empty timeline.
        while (_doc.CanUndo) _doc.Undo();
        Assert.Empty(_doc.Project.Timelines);
    }
}
