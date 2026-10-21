using PixelTuner.Core.Model;
using PixelTuner.Package;
using PixelTuner.UI.ViewModels;

namespace PixelTuner.UI.Tests;

/// <summary>The controls of a timeline header: each is there only where the .bin of that kind has a field for it.</summary>
public sealed class EditorTimelineHeaderTests : IDisposable
{
    private readonly ProjectDocument _doc = ProjectDocument.CreateNew("Show");
    private readonly EditorViewModel _editor;

    public EditorTimelineHeaderTests() => _editor = new EditorViewModel(_doc);

    public void Dispose() => _editor.Dispose();

    private Timeline Add(string kind)
    {
        switch (kind)
        {
            case TimelineKind.Pixel: _editor.AddPixelTimelineCommand.Execute(null); break;
            case TimelineKind.Video: _editor.AddVideoTimelineCommand.Execute(null); break;
            case TimelineKind.EpicPoi: _editor.AddEpicTimelineCommand.Execute(null); break;
            default: _editor.AddLightTimelineCommand.Execute(null); break;
        }
        return _doc.Project.Timelines[^1];   // adding selects the new timeline
    }

    [Theory]
    [InlineData(TimelineKind.Pixel, true, false, true, false, false)]
    [InlineData(TimelineKind.Video, false, false, false, true, true)]
    [InlineData(TimelineKind.EpicPoi, false, false, false, false, false)]
    [InlineData(TimelineKind.Light, false, true, false, false, true)]
    public void Each_kind_shows_only_the_controls_it_has_a_field_for(string kind, bool flip, bool monochrome, bool height, bool layout, bool colourIsALabel)
    {
        Add(kind);

        Assert.True(_editor.IsTimelineSelected);
        Assert.Equal(flip, _editor.ShowFlip);
        Assert.Equal(monochrome, _editor.ShowMonochrome);
        Assert.Equal(height, _editor.ShowHeight);
        Assert.Equal(layout, _editor.ShowLayout);
        Assert.Equal(colourIsALabel, _editor.ColorIsOnlyALabel);   // the device does not store the colour; the panel says so
    }

    [Fact]
    public void Colour_is_typed_or_picked_and_undone()
    {
        var timeline = Add(TimelineKind.Pixel);
        string before = timeline.Color;

        _editor.TimelineColorText = "#D1495B";
        Assert.Equal("#FFD1495B", timeline.Color);
        Assert.Equal("#D1495B", _editor.TimelineColorText);

        _editor.Palette[1].Pick.Execute(null);
        Assert.Equal("#FF2FA84F", timeline.Color);

        _editor.TimelineColorText = "not a colour";
        Assert.Equal("#FF2FA84F", timeline.Color);
        Assert.NotNull(_editor.Message);

        _doc.Undo();
        _doc.Undo();
        Assert.Equal(before, timeline.Color);
    }

    [Fact]
    public void Flip_and_monochrome_are_edits_of_their_timelines()
    {
        var pixel = Add(TimelineKind.Pixel);
        _editor.FlipVertical = true;
        Assert.True(pixel.FlipVertical);
        _doc.Undo();
        Assert.False(pixel.FlipVertical);

        var light = Add(TimelineKind.Light);
        _editor.Monochrome = true;
        Assert.True(light.Monochrome);
        _editor.FlipVertical = true;   // a light timeline has no flip: nothing happens
        Assert.False(light.FlipVertical);
        _doc.Undo();
        Assert.False(light.Monochrome);
    }

    [Fact]
    public void Height_is_set_on_the_timeline_and_refused_while_it_holds_pictures_of_another_height()
    {
        var timeline = Add(TimelineKind.Pixel);
        Assert.Contains("first picture", _editor.HeightText);

        _editor.TimelineHeight = 36;
        Assert.Equal(36, timeline.Height);
        Assert.Contains("36", _editor.HeightText);

        timeline.Items.Add(new TimelineItem { MediaWidth = 5, MediaHeight = 36, Duration = TimeSpan.FromSeconds(1) });
        _editor.TimelineHeight = 80;
        Assert.Equal(36, timeline.Height);
        Assert.NotNull(_editor.Message);

        _editor.TimelineHeight = 0;   // back to "the first picture decides"
        Assert.Equal(0, timeline.Height);
        Assert.Contains("36", _editor.HeightText);
    }

    [Fact]
    public void A_video_timeline_picks_one_of_the_layouts_of_the_project()
    {
        var first = new LedLayout { Name = "Glasses", Columns = 2, Rows = 2, Leds = [0, 1] };
        var second = new LedLayout { Name = "", Columns = 2, Rows = 2, Leds = [2, 3] };
        _doc.Project.LedLayouts.AddRange([first, second]);
        var timeline = Add(TimelineKind.Video);

        Assert.Equal(3, _editor.LayoutNames.Count);
        Assert.Equal("Glasses", _editor.LayoutNames[1]);
        Assert.Equal(0, _editor.LayoutIndex);

        _editor.LayoutIndex = 2;
        Assert.Equal(second.Id, timeline.LedLayoutId);
        _editor.LayoutIndex = 0;
        Assert.Null(timeline.LedLayoutId);
        _doc.Undo();
        Assert.Equal(second.Id, timeline.LedLayoutId);
    }

    [Fact]
    public void Timelines_move_up_and_down_and_the_buttons_know_the_ends()
    {
        var a = Add(TimelineKind.Pixel);
        var b = Add(TimelineKind.Video);
        var c = Add(TimelineKind.Light);

        Assert.True(_editor.MoveTimelineUpCommand.CanExecute(null));
        Assert.False(_editor.MoveTimelineDownCommand.CanExecute(null));   // the last one

        _editor.MoveTimelineUpCommand.Execute(null);
        Assert.Equal([a, c, b], _doc.Project.Timelines);
        _editor.MoveTimelineUpCommand.Execute(null);
        Assert.False(_editor.MoveTimelineUpCommand.CanExecute(null));      // now the first
        Assert.Equal([c, a, b], _doc.Project.Timelines);

        _doc.Undo();
        _doc.Undo();
        Assert.Equal([a, b, c], _doc.Project.Timelines);
    }
}
