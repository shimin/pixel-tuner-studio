using PixelTuner.Core.Editing;
using PixelTuner.Core.Export;
using PixelTuner.Core.Imaging;
using PixelTuner.Core.Model;
using PixelTuner.Package;
using PixelTuner.UI.PreviewPanel;
using PixelTuner.UI.ViewModels;
using SkiaSharp;

namespace PixelTuner.UI.Tests;

public sealed class PreviewViewModelTests : IDisposable
{
    private readonly ProjectDocument _doc = ProjectDocument.CreateNew("Show");
    private readonly EditorViewModel _editor;
    private PreviewViewModel Preview => _editor.Preview;

    public PreviewViewModelTests() => _editor = new EditorViewModel(_doc);

    public void Dispose()
    {
        _editor.Dispose();
        _doc.Dispose();
    }

    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);
    private static TimeSpan Ms(double milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    private string Media(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                bitmap.SetPixel(x, y, new SKColor((byte)(10 + x * 30), (byte)(20 + y * 50), (byte)(30 + (x + y) * 7)));
        using var image = SKImage.FromBitmap(bitmap);
        return _doc.AddMedia(image.Encode(SKEncodedImageFormat.Png, 100).ToArray(), $"p{width}x{height}.png");
    }

    private TimelineItem PictureOn(Timeline timeline, int width, int height, double start, double length, int stretching = 20)
    {
        var item = new TimelineItem
        {
            Kind = ItemKind.Image, ResourceId = Media(width, height), SourcePath = "p.png", MediaWidth = width, MediaHeight = height,
            Start = S(start), Duration = S(length), Stretching = stretching,
        };
        _doc.Execute(Edits.AddItem(timeline, item));
        return item;
    }

    private Timeline AddTimeline(string kind)
    {
        var timeline = new Timeline { Kind = kind, Name = kind };
        _doc.Execute(Edits.AddTimeline(_doc.Project, timeline));
        return timeline;
    }

    // ---- what is shown

    [Fact]
    public void With_nothing_selected_there_is_nothing_to_show_and_the_panel_says_what_to_do()
    {
        Assert.Equal(PreviewMode.None, Preview.Mode);
        Assert.Null(Preview.Frame);
        Assert.True(Preview.HasProblem);
        Assert.NotEmpty(Preview.Problem!);
        Assert.Equal("", Preview.Info);
    }

    [Fact]
    public void A_pixel_timeline_is_a_poi_by_default_and_a_hoop_when_asked_and_the_frame_follows_the_playhead()
    {
        var timeline = AddTimeline(TimelineKind.Pixel);
        PictureOn(timeline, 4, 3, 0, 1, stretching: 20);                   // 1 ms a column
        _editor.Timelines.Select(timeline);

        Assert.Equal(PreviewMode.Poi, Preview.Mode);
        Assert.True(Preview.IsPixel);
        Assert.False(Preview.IsHoop);
        Assert.False(Preview.HasProblem);
        Assert.Equal(3, Preview.Current!.Height);

        _editor.Timelines.SetPlayhead(Ms(0.5));
        Assert.Equal(0, Preview.Frame!.Column);
        _editor.Timelines.SetPlayhead(Ms(2.5));
        Assert.Equal(2, Preview.Frame!.Column);
        Assert.Contains("3", Preview.Info);                                 // column 3 of 4

        Preview.PixelView = 1;
        Assert.Equal(PreviewMode.Hoop, Preview.Mode);
        Assert.True(Preview.AsHoop);
        Assert.True(Preview.IsHoop);
        Preview.AsHoop = false;
        Assert.Equal(0, Preview.PixelView);
    }

    [Fact]
    public void The_preview_pixels_are_the_picture_the_item_plays_as_the_device_gets_it()
    {
        var timeline = AddTimeline(TimelineKind.Pixel);
        PictureOn(timeline, 4, 3, 0, 1, stretching: 20);
        _editor.Timelines.Select(timeline);
        _editor.Timelines.SetPlayhead(Ms(1.5));                             // column 1: x = 1

        var frame = Preview.Frame!;
        Assert.Equal(1, frame.Column);
        // The red of the picture changes along it and the green down it, so a wrong column or row would show. (The device's bytes are the
        // picture after PixelTuner 2's colour correction, which is why they are not the source colours; Core's tests compare them with the file.)
        Assert.Equal(frame.Colors[0].R, frame.Colors[2].R);
        Assert.True(frame.Colors[2].G > frame.Colors[0].G);
        _editor.Timelines.SetPlayhead(Ms(2.5));
        Assert.True(Preview.Frame!.Colors[0].R > frame.Colors[0].R);
    }

    [Fact]
    public void Selecting_an_item_shows_the_timeline_it_is_on()
    {
        var timeline = AddTimeline(TimelineKind.Pixel);
        var item = PictureOn(timeline, 4, 3, 0, 1);
        _editor.Timelines.Select(item);
        Assert.Same(timeline, Preview.Timeline);
        Assert.Contains("pixel", Preview.Name);
    }

    [Fact]
    public void The_preview_is_built_again_when_the_project_changes()
    {
        var timeline = AddTimeline(TimelineKind.Pixel);
        var item = PictureOn(timeline, 4, 3, 0, 1, stretching: 20);
        _editor.Timelines.Select(timeline);
        Assert.Equal(Ms(1), Preview.Frame!.ColumnTime);

        _doc.Execute(Edits.SetStretching(item, 40));
        Assert.Equal(Ms(2), Preview.Frame!.ColumnTime);
        _doc.Undo();
        Assert.Equal(Ms(1), Preview.Frame!.ColumnTime);
    }

    [Fact]
    public void The_view_is_told_when_the_playhead_moves_the_selection_changes_or_the_project_changes()
    {
        var timeline = AddTimeline(TimelineKind.Pixel);
        var item = PictureOn(timeline, 4, 3, 0, 1);
        int changes = 0;
        Preview.Changed += (_, _) => changes++;

        _editor.Timelines.Select(timeline);
        Assert.True(changes > 0);
        int before = changes;
        _editor.Timelines.SetPlayhead(Ms(3));
        Assert.True(changes > before);
        before = changes;
        _doc.Execute(Edits.SetStretching(item, 30));
        Assert.True(changes > before);
    }

    // ---- timelines that cannot be shown

    [Fact]
    public void A_timeline_with_nothing_on_it_has_nothing_to_show()
    {
        var timeline = AddTimeline(TimelineKind.Pixel);
        _editor.Timelines.Select(timeline);
        Assert.Equal(PreviewMode.None, Preview.Mode);
        Assert.NotEmpty(Preview.Problem!);
    }

    [Fact]
    public void A_video_timeline_without_a_layout_says_so_and_with_one_shows_the_frame_on_its_leds()
    {
        string gif = Path.Combine(AppContext.BaseDirectory, "Fixtures", "glasses", "glasses.gif");
        var timeline = AddTimeline(TimelineKind.Video);
        var item = new TimelineItem
        {
            Kind = ItemKind.Video, ResourceId = _doc.AddMedia(gif), SourcePath = "glasses.gif", Start = S(0), Duration = S(2), Stretching = 40,
        };
        _doc.Execute(Edits.AddItem(timeline, item));
        _editor.Timelines.Select(timeline);

        Assert.Equal(PreviewMode.None, Preview.Mode);
        Assert.Contains(EditorViewModel.IssueText(new BuildIssue(BuildSeverity.Error, BuildIssueCode.NoLedLayout)), Preview.Problem);

        var layout = new LedLayout { Name = "prop", Columns = 3, Rows = 2, Leds = [0, 1, 2, 5, 4, 3] };
        _doc.Execute(Edits.AddLedLayout(_doc.Project, layout, timeline));
        Assert.Equal(PreviewMode.Video, Preview.Mode);
        Assert.Same(layout, Preview.Layout);
        Assert.Equal(6, Preview.Frame!.Colors.Count);
        Assert.Equal(Ms(40), Preview.Frame.ColumnTime);

        // The canvas fills the outline of the prop with the same colours.
        _editor.Timelines.SetPlayhead(Ms(100));
        Assert.Equal(Preview.Frame.Colors, Preview.ColoursOn(layout));
    }

    [Fact]
    public void No_colours_are_given_for_a_layout_no_video_plays_on()
    {
        var layout = new LedLayout { Name = "prop", Columns = 2, Rows = 2, Leds = [0, 1] };
        _doc.Execute(Edits.AddLedLayout(_doc.Project, layout));
        Assert.Null(Preview.ColoursOn(layout));
    }

    [Fact]
    public void Light_shows_its_colours()
    {
        var timeline = AddTimeline(TimelineKind.Light);
        _doc.Execute(Edits.AddItem(timeline, new TimelineItem { Kind = ItemKind.Solid, Color = "#FF2040A0", Start = S(0), Duration = S(1) }));
        _editor.Timelines.Select(timeline);
        _editor.Timelines.SetPlayhead(S(0.5));

        Assert.Equal(PreviewMode.Colours, Preview.Mode);
        Assert.Contains(new Rgb(0x20, 0x40, 0xA0), Preview.Frame!.Colors);
        Assert.False(Preview.IsPixel);
    }

    [Fact]
    public void The_swing_and_the_afterglow_settings_have_defaults_and_redraw_the_view()
    {
        Assert.Equal(6, Preview.SwingSpeed);
        int changes = 0;
        Preview.Changed += (_, _) => changes++;
        Preview.SwingSpeed = 3;
        Preview.LedPitch = 20;
        Preview.Tail = 200;
        Preview.OrbitSpeed = 2;
        Assert.Equal(4, changes);
    }
}
