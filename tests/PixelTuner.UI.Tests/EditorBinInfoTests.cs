using PixelTuner.Core.Editing;
using PixelTuner.Core.Model;
using PixelTuner.Package;
using PixelTuner.UI.ViewModels;
using SkiaSharp;

namespace PixelTuner.UI.Tests;

/// <summary>The inspector's lines about the .bin: entry, block, file and memory, the warnings, and the time of a column or frame.</summary>
public sealed class EditorBinInfoTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pt-bininfo").FullName;
    private readonly ProjectDocument _doc = ProjectDocument.CreateNew("Show");
    private readonly FakePicker _picker = new();
    private readonly EditorViewModel _editor;

    public EditorBinInfoTests() => _editor = new EditorViewModel(_doc, null, () => _picker);

    public void Dispose()
    {
        _editor.Dispose();
        Directory.Delete(_dir, true);
    }

    private string Picture(string name, int width, int height, byte red)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(new SKColor(red, 100, 50));
        using var image = SKImage.FromBitmap(bitmap);
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, image.Encode(SKEncodedImageFormat.Png, 100).ToArray());
        return path;
    }

    private async Task<Timeline> TwoPictures()
    {
        _editor.AddPixelTimelineCommand.Execute(null);
        var timeline = _doc.Project.Timelines[0];
        _picker.ImagePath = Picture("a.png", 6, 4, 200);
        await _editor.AddItemCommand.ExecuteAsync(null);
        _editor.Timelines.SetPlayhead(TimeSpan.FromSeconds(10));
        await _editor.AddItemCommand.ExecuteAsync(null);   // the same file again
        return timeline;
    }

    [Fact]
    public async Task The_inspector_says_which_entry_and_block_the_item_is_and_who_shares_it()
    {
        var timeline = await TwoPictures();   // entries: picture, pause, picture
        _editor.Timelines.Select(timeline.Items[1]);

        Assert.True(_editor.HasBinInfo);
        Assert.Contains("3", _editor.BinLine);      // entry 3 of 3
        Assert.Contains("shared with 1", _editor.BinLine);
        Assert.Contains("file", _editor.BinLine);

        // Another brightness is a block of its own, and the line follows the edit.
        _editor.Brightness = 50;
        Assert.DoesNotContain("shared", _editor.BinLine);
    }

    [Fact]
    public async Task The_file_is_shown_against_the_memory_of_the_device_that_plays_the_timeline()
    {
        var timeline = await TwoPictures();
        var device = new DeviceBinding { DeviceType = DeviceKind.Poi, LedCount = 4, MemoryBytes = 3_800_000, TimelineIds = [timeline.Id] };
        _doc.Execute(Edits.AddDevice(_doc.Project, device));
        _editor.Timelines.Select(timeline.Items[0]);

        Assert.Contains("3.6 MB", _editor.BinLine.Replace(',', '.'));
        Assert.Contains("%", _editor.BinLine);
    }

    [Fact]
    public async Task A_file_that_does_not_fit_the_device_is_an_error_line()
    {
        var timeline = await TwoPictures();
        var device = new DeviceBinding { DeviceType = DeviceKind.Poi, LedCount = 4, MemoryBytes = 100, TimelineIds = [timeline.Id] };
        _doc.Execute(Edits.AddDevice(_doc.Project, device));
        _editor.Timelines.Select(timeline.Items[0]);

        Assert.True(_editor.HasBinIssues);
        Assert.Contains("memory", _editor.BinIssues[0]);
        Assert.Contains("cannot be built", _editor.BinLine);
    }

    [Fact]
    public void A_light_item_that_is_not_a_whole_number_of_steps_gets_a_warning_in_the_inspector()
    {
        _editor.AddLightTimelineCommand.Execute(null);
        _editor.AddColorCommand.Execute(null);
        var item = _doc.Project.Timelines[0].Items[0];

        Assert.False(_editor.HasBinIssues);   // 5 s is a whole number of 5 ms steps

        _editor.LengthText = "1.003";
        Assert.Equal(TimeSpan.FromMilliseconds(1003), item.Duration);
        Assert.True(_editor.HasBinIssues);
        Assert.Contains("light steps", _editor.BinIssues[0]);
        Assert.False(_editor.BinInfo!.EntryNumber.HasValue);   // light has samples, not entries
    }

    [Fact]
    public async Task A_missing_file_says_so_for_the_item()
    {
        var timeline = await TwoPictures();
        timeline.Items[0].ResourceId = null;
        _editor.Timelines.Select(timeline.Items[0]);

        Assert.Contains("cannot be built", _editor.BinLine);
        Assert.Contains("missing", _editor.BinIssues.Single());
    }

    [Fact]
    public async Task The_time_of_a_column_or_a_frame_follows_the_stretching()
    {
        var timeline = await TwoPictures();
        _editor.Timelines.Select(timeline.Items[0]);

        Assert.Contains("200", _editor.ColumnTimeText);   // 4 x 50 us
        _editor.Stretching = 9;
        Assert.Contains("450", _editor.ColumnTimeText);
        Assert.Contains("µs", _editor.ColumnTimeText);

        timeline.Items[0].Kind = ItemKind.Video;
        timeline.Items[0].Stretching = 40;
        _editor.Timelines.Select(null);
        _editor.Timelines.Select(timeline.Items[0]);
        Assert.Contains("40 ms", _editor.ColumnTimeText);
        Assert.Contains("25", _editor.ColumnTimeText);   // frames a second

        timeline.Items[0].Kind = ItemKind.Gradient;
        timeline.Items[0].Stretching = 5;
        _editor.Timelines.Select(null);
        _editor.Timelines.Select(timeline.Items[0]);
        Assert.Contains("1 ms", _editor.ColumnTimeText);   // E-Pic: k / 5 ms
    }

    [Fact]
    public void Nothing_is_shown_without_an_item()
    {
        _editor.AddPixelTimelineCommand.Execute(null);
        Assert.False(_editor.HasBinInfo);
        Assert.Equal("", _editor.BinLine);
        Assert.Empty(_editor.BinIssues);
        Assert.Equal("", _editor.ColumnTimeText);
    }
}
