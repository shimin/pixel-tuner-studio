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

        await _editor.AddItemCommand.ExecuteAsync(null);

        var item = Assert.Single(_doc.Project.Timelines[0].Items);
        Assert.Equal(ItemKind.Image, item.Kind);
        Assert.Equal(TimeSpan.FromSeconds(3), item.Start);
        Assert.Equal(EditorViewModel.DefaultItemLength, item.Duration);
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

        await _editor.AddItemCommand.ExecuteAsync(null);   // playhead 0: five seconds do not fit before 7

        Assert.Equal([0d, 7d], timeline.Items.Select(i => i.Start.TotalSeconds));
        Assert.Equal(5, timeline.Items[0].Duration.TotalSeconds);

        _editor.Timelines.SetPlayhead(TimeSpan.FromSeconds(6));
        _editor.Timelines.Select(timeline);
        await _editor.AddItemCommand.ExecuteAsync(null);   // inside nothing: 6 to 7 is a second

        Assert.Equal(1, timeline.Items.First(i => i.Start == TimeSpan.FromSeconds(6)).Duration.TotalSeconds);
    }

    [Fact]
    public async Task Add_image_needs_a_pixel_timeline_and_an_image()
    {
        _picker.ImagePath = WriteBmp();
        await _editor.AddItemCommand.ExecuteAsync(null);
        Assert.NotNull(_editor.Message);

        _editor.AddLightTimelineCommand.Execute(null);
        await _editor.AddItemCommand.ExecuteAsync(null);
        Assert.Empty(_doc.Project.Timelines[0].Items);

        _editor.AddPixelTimelineCommand.Execute(null);
        string notAnImage = Path.Combine(_dir, "x.png");
        File.WriteAllText(notAnImage, "hello");
        _picker.ImagePath = notAnImage;
        await _editor.AddItemCommand.ExecuteAsync(null);
        Assert.Empty(_doc.Project.Timelines[1].Items);
        Assert.Contains("x.png", _editor.Message);
    }

    [Fact]
    public async Task Inspector_edits_brightness_stretching_and_laser_through_undo()
    {
        _editor.AddPixelTimelineCommand.Execute(null);
        _picker.ImagePath = WriteBmp();
        await _editor.AddItemCommand.ExecuteAsync(null);
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
        Assert.Equal((100, 4, false), (item.Brightness, item.Stretching, item.LaserOn));   // 4 is PixelTuner 2's default
    }

    private string WriteGif()
    {
        byte[] gif =
        [
            0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 1, 0, 1, 0, 0x80, 0, 0, 0, 0, 0, 0xFF, 0xFF, 0xFF,
            0x2C, 0, 0, 0, 0, 1, 0, 1, 0, 0, 2, 2, 0x44, 1, 0, 0x3B,
        ];
        string path = Path.Combine(_dir, "anim.gif");
        File.WriteAllBytes(path, gif);
        return path;
    }

    [Fact]
    public async Task Files_go_to_the_timeline_kind_that_takes_them()
    {
        _editor.AddVideoTimelineCommand.Execute(null);
        _picker.GifPath = WriteGif();
        await _editor.AddItemCommand.ExecuteAsync(null);
        var video = Assert.Single(_doc.Project.Timelines[0].Items);
        Assert.Equal(ItemKind.Video, video.Kind);
        Assert.Equal(40, video.Stretching);   // 25 frames a second
        Assert.Equal("Frame time, ms", _editor.StretchingLabel);
        Assert.True(_editor.IsVideoSelected);
        _editor.OffsetX = 12;
        _editor.OffsetY = -3;
        Assert.Equal((12, -3), (video.OffsetX, video.OffsetY));

        _editor.AddEpicTimelineCommand.Execute(null);
        _picker.ImagePath = WriteBmp();
        await _editor.AddItemCommand.ExecuteAsync(null);
        var gradient = Assert.Single(_doc.Project.Timelines[1].Items);
        Assert.Equal(ItemKind.Gradient, gradient.Kind);
        Assert.True(_editor.ShowStretching);
        Assert.False(_editor.ShowLaser);

        // A bmp is not an animation.
        _editor.AddVideoTimelineCommand.Execute(null);
        _picker.GifPath = _picker.ImagePath;
        await _editor.AddItemCommand.ExecuteAsync(null);
        Assert.Empty(_doc.Project.Timelines[2].Items);
        Assert.NotNull(_editor.Message);
    }

    [Fact]
    public void Light_timeline_takes_colours_and_effects_and_the_inspector_edits_them()
    {
        _editor.AddLightTimelineCommand.Execute(null);
        _editor.AddColorCommand.Execute(null);
        var solid = _doc.Project.Timelines[0].Items[0];
        Assert.Equal((ItemKind.Solid, "#FFFFFF"), (solid.Kind, solid.Color));
        Assert.True(_editor.IsLightItem);
        Assert.False(_editor.IsEffectSelected);

        _editor.ItemColor = "#102030";
        Assert.Equal("#102030", solid.Color);
        _editor.ItemColor = "purple";   // refused, the old colour stays
        Assert.Equal("#102030", solid.Color);
        Assert.Contains("purple", _editor.Message);

        _editor.Timelines.SetPlayhead(solid.End);
        _editor.AddEffectCommand.Execute(null);
        var effect = _doc.Project.Timelines[0].Items[1];
        Assert.True(_editor.IsEffectSelected);
        _editor.EffectIndex = 2;
        _editor.T1 = 300;
        _editor.ItemColorTo = "#FF0000";
        Assert.Equal((LightEffectKind.SquareWave, 300, "#FF0000"), (effect.Effect, effect.T1, effect.ColorLast));
        Assert.Equal(2, _editor.EffectIndex);
    }

    [Fact]
    public void Colours_and_effects_do_not_go_to_a_pixel_timeline_and_files_do_not_go_to_a_light_one()
    {
        _editor.AddPixelTimelineCommand.Execute(null);
        _editor.AddColorCommand.Execute(null);
        Assert.Empty(_doc.Project.Timelines[0].Items);
        Assert.NotNull(_editor.Message);

        _editor.AddLightTimelineCommand.Execute(null);
        _picker.ImagePath = WriteBmp();
        _editor.AddItemCommand.Execute(null);
        Assert.Empty(_doc.Project.Timelines[1].Items);
    }

    [Fact]
    public async Task Start_and_length_are_typed_in_the_inspector_and_refused_when_they_do_not_fit()
    {
        _editor.AddPixelTimelineCommand.Execute(null);
        var timeline = _doc.Project.Timelines[0];
        timeline.Items.Add(new TimelineItem { Start = TimeSpan.FromSeconds(8), Duration = TimeSpan.FromSeconds(1) });
        _picker.ImagePath = WriteBmp();
        await _editor.AddItemCommand.ExecuteAsync(null);   // 0 to 5
        var item = timeline.Items[0];

        _editor.StartText = "0:01.500";
        Assert.Equal(1.5, item.Start.TotalSeconds, 6);
        _editor.LengthText = "7";   // 1.5 + 7 would run into the item at 8
        Assert.Equal(5, item.Duration.TotalSeconds, 6);
        Assert.Contains("7", _editor.Message);
        Assert.Equal("0:05.000", _editor.LengthText);

        _editor.LengthText = "6.5";
        Assert.Equal(6.5, item.Duration.TotalSeconds, 6);
        _editor.StartText = "nonsense";
        Assert.Equal(1.5, item.Start.TotalSeconds, 6);

        _doc.Undo();
        _doc.Undo();
        Assert.Equal((0d, 5d), (item.Start.TotalSeconds, item.Duration.TotalSeconds));
    }

    [Fact]
    public void Marker_position_is_typed_in_the_inspector()
    {
        _editor.AddMarkerCommand.Execute(null);
        var marker = _doc.Project.Markers[0];
        _editor.PositionText = "0:02.000";
        Assert.Equal(2, marker.Position.TotalSeconds, 6);
        _editor.PositionText = "oops";
        Assert.Equal(2, marker.Position.TotalSeconds, 6);
        Assert.Equal("0:02.000", _editor.PositionText);
    }
}
