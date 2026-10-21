using PixelTuner.Core.Model;
using PixelTuner.Package;
using PixelTuner.UI.Services;
using PixelTuner.UI.ViewModels;

namespace PixelTuner.UI.Tests;

public sealed class EditorItemTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pt-items").FullName;
    private readonly ProjectDocument _doc = ProjectDocument.CreateNew("Show");
    private readonly FakePicker _picker = new();
    private readonly EditorViewModel _editor;

    public EditorItemTests() => _editor = new EditorViewModel(_doc, null, () => _picker);

    public void Dispose()
    {
        _editor.Dispose();
        Directory.Delete(_dir, true);
    }

    /// <summary>A 2 x 1 24-bit BMP: one red and one blue pixel.</summary>
    private string WriteBmp(string name = "pic.bmp")
    {
        byte[] bmp =
        [
            0x42, 0x4D, 62, 0, 0, 0, 0, 0, 0, 0, 54, 0, 0, 0,
            40, 0, 0, 0, 2, 0, 0, 0, 1, 0, 0, 0, 1, 0, 24, 0, 0, 0, 0, 0, 8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 255, 255, 0, 0, 0, 0,
        ];
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bmp);
        return path;
    }

    [Fact]
    public async Task Add_image_puts_it_at_the_playhead_of_the_selected_pixel_timeline()
    {
        _editor.AddPixelTimelineCommand.Execute(null);
        _editor.Timelines.SetPlayhead(TimeSpan.FromSeconds(3));
        _picker.ImagePath = WriteBmp();

        await _editor.AddImageCommand.ExecuteAsync(null);

        var item = Assert.Single(_doc.Project.Timelines[0].Items);
        Assert.Equal(ItemKind.Image, item.Kind);
        Assert.Equal(TimeSpan.FromSeconds(3), item.Start);
        Assert.Equal(EditorViewModel.DefaultImageLength, item.Duration);
        Assert.True(_doc.HasMedia(item.ResourceId));
        Assert.Same(item, _editor.Timelines.Selected);
        Assert.True(_editor.HasImage);

        _doc.Undo();
        Assert.Empty(_doc.Project.Timelines[0].Items);
    }

    [Fact]
    public async Task Second_image_goes_after_the_first_and_is_cut_to_the_gap()
    {
        _editor.AddPixelTimelineCommand.Execute(null);
        var timeline = _doc.Project.Timelines[0];
        timeline.Items.Add(new TimelineItem { Start = TimeSpan.FromSeconds(7), Duration = TimeSpan.FromSeconds(2) });
        _picker.ImagePath = WriteBmp();

        await _editor.AddImageCommand.ExecuteAsync(null);   // playhead 0: five seconds do not fit before 7

        Assert.Equal([0d, 7d], timeline.Items.Select(i => i.Start.TotalSeconds));
        Assert.Equal(5, timeline.Items[0].Duration.TotalSeconds);

        _editor.Timelines.SetPlayhead(TimeSpan.FromSeconds(6));
        _editor.Timelines.Select(timeline);
        await _editor.AddImageCommand.ExecuteAsync(null);   // inside nothing: 6 to 7 is a second

        Assert.Equal(1, timeline.Items.First(i => i.Start == TimeSpan.FromSeconds(6)).Duration.TotalSeconds);
    }

    [Fact]
    public async Task Add_image_needs_a_pixel_timeline_and_an_image()
    {
        _picker.ImagePath = WriteBmp();
        await _editor.AddImageCommand.ExecuteAsync(null);
        Assert.NotNull(_editor.Message);

        _editor.AddLightTimelineCommand.Execute(null);
        await _editor.AddImageCommand.ExecuteAsync(null);
        Assert.Empty(_doc.Project.Timelines[0].Items);

        _editor.AddPixelTimelineCommand.Execute(null);
        string notAnImage = Path.Combine(_dir, "x.png");
        File.WriteAllText(notAnImage, "hello");
        _picker.ImagePath = notAnImage;
        await _editor.AddImageCommand.ExecuteAsync(null);
        Assert.Empty(_doc.Project.Timelines[1].Items);
        Assert.Contains("x.png", _editor.Message);
    }

    [Fact]
    public async Task Inspector_edits_brightness_stretching_and_laser_through_undo()
    {
        _editor.AddPixelTimelineCommand.Execute(null);
        _picker.ImagePath = WriteBmp();
        await _editor.AddImageCommand.ExecuteAsync(null);
        var item = _doc.Project.Timelines[0].Items[0];

        _editor.Brightness = 40;
        _editor.Stretching = 3;
        _editor.LaserOn = true;
        Assert.Equal((40, 3, true), (item.Brightness, item.Stretching, item.LaserOn));
        Assert.Equal(40, _editor.Brightness);

        _editor.Stretching = 0;   // out of range: ignored
        Assert.Equal(3, item.Stretching);

        _doc.Undo();
        _doc.Undo();
        _doc.Undo();
        Assert.Equal((100, 1, false), (item.Brightness, item.Stretching, item.LaserOn));
    }
}
