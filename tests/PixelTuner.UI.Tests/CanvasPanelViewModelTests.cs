using PixelTuner.Core.Editing;
using PixelTuner.Core.Layout;
using PixelTuner.Core.Model;
using PixelTuner.Package;
using PixelTuner.UI.LedLayouts;
using PixelTuner.UI.ViewModels;

namespace PixelTuner.UI.Tests;

public sealed class CanvasPanelViewModelTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pt-canvas").FullName;
    private readonly ProjectDocument _doc = ProjectDocument.CreateNew("Show");
    private readonly FakePicker _picker = new();
    private readonly EditorViewModel _editor;

    private const string TwoShapes = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">
          <rect id="left" x="0" y="0" width="40" height="100"/>
          <rect id="right" x="60" y="0" width="40" height="100"/>
        </svg>
        """;

    public CanvasPanelViewModelTests()
    {
        CanvasPanelViewModel.ForgetCopiedMarkup();
        _editor = new EditorViewModel(_doc, null, () => _picker);
    }

    public void Dispose()
    {
        _editor.Dispose();
        _doc.Dispose();
        Directory.Delete(_dir, true);
    }

    private CanvasPanelViewModel Canvas => _editor.Canvas;

    private string Svg(string name, string text)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllText(path, text);
        return path;
    }

    private void Import(string text = TwoShapes)
    {
        _picker.SvgPath = Svg("props.svg", text);
        Canvas.ImportSvgCommand.ExecuteAsync(null).GetAwaiter().GetResult();
    }

    private CanvasElementViewModel Element(string id) => Canvas.Elements.Single(e => e.Id == id);

    // Marks a layout up for an outline the way the window does, through its view model.
    private void Mark(string id, params (int X, int Y)[] leds)
    {
        Canvas.Edit(Element(id));
        var window = _editor.MarkupEditor!;
        foreach (var (x, y) in leds) { window.Press(x, y, GestureKeys.None); window.Release(x, y); }
        window.ApplyCommand.Execute(null);
    }

    // ---- the drawing of the project

    [Fact]
    public void A_new_project_has_no_drawing_and_a_built_in_one_can_be_taken_and_taken_away_with_undo()
    {
        Assert.True(Canvas.HasNoDrawing);
        Assert.Empty(Canvas.Elements);

        Canvas.UseSchemaCommand.Execute("human");
        Assert.True(Canvas.HasDrawing);
        Assert.Equal(12, Canvas.Elements.Count);
        Assert.NotNull(_doc.Project.CanvasResourceId);
        Assert.True(_doc.HasMedia(_doc.Project.CanvasResourceId));
        Assert.True(_doc.IsDirty);

        Canvas.RemoveDrawingCommand.Execute(null);
        Assert.True(Canvas.HasNoDrawing);

        _doc.Undo();
        Assert.Equal(12, Canvas.Elements.Count);
        _doc.Undo();
        Assert.True(Canvas.HasNoDrawing);
    }

    [Fact]
    public void An_svg_file_is_imported_as_the_drawing_of_the_project()
    {
        Import();
        Assert.Equal(["left", "right"], Canvas.Elements.Select(e => e.Id));
        Assert.Null(Canvas.Message);
        Assert.Equal((0, 0, 100, 100), Canvas.Page);
    }

    [Fact]
    public void A_file_that_is_not_an_svg_or_has_no_shapes_is_refused_with_a_message_and_the_project_is_left_alone()
    {
        Import("this is not xml");
        Assert.True(Canvas.HasNoDrawing);
        Assert.Contains("props.svg", Canvas.Message);
        Assert.False(_doc.IsDirty);

        Canvas.UseSchemaCommand.Execute("human");
        Import("""<svg xmlns="http://www.w3.org/2000/svg"><defs/></svg>""");
        Assert.Equal(12, Canvas.Elements.Count);       // the drawing that was there stays
        Assert.NotNull(Canvas.Message);
    }

    [Fact]
    public void A_cancelled_import_changes_nothing()
    {
        _picker.SvgPath = null;
        Canvas.ImportSvgCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.True(Canvas.HasNoDrawing);
        Assert.False(_doc.IsDirty);
    }

    [Fact]
    public void The_drawing_is_read_again_when_the_project_changes_under_it()
    {
        Import();
        string id = _doc.Project.CanvasResourceId!;
        _doc.Undo();
        Assert.True(Canvas.HasNoDrawing);
        _doc.Redo();
        Assert.Equal(id, _doc.Project.CanvasResourceId);
        Assert.Equal(2, Canvas.Elements.Count);
    }

    // ---- picking

    [Fact]
    public void A_point_of_the_drawing_picks_the_outline_under_it_and_says_what_it_is()
    {
        Import();
        Canvas.SelectAt(20, 50);
        Assert.Equal("left", Canvas.Selected!.Id);
        Canvas.SelectAt(80, 50);
        Assert.Equal("right", Canvas.Selected!.Id);
        Canvas.SelectAt(50, 50);                        // between them
        Assert.Null(Canvas.Selected);

        Canvas.Hover(20, 50);
        Assert.Contains("left", Canvas.HoverText);
        Canvas.Hover(50, 50);
        Assert.Equal("", Canvas.HoverText);
    }

    [Fact]
    public void The_selection_stays_on_its_outline_when_the_project_changes()
    {
        Import();
        Canvas.SelectAt(20, 50);
        Mark("right", (0, 0));
        Assert.Equal("right", Canvas.Selected!.Id);     // the double click selected it, and the markup did not drop it
    }

    // ---- the markup of an outline

    [Fact]
    public void Marking_up_an_outline_opens_the_window_with_its_shape_and_OK_makes_the_layout_named_like_it()
    {
        Import();
        Canvas.Edit(Element("left"));
        var window = _editor.MarkupEditor;
        Assert.NotNull(window);
        Assert.Equal("left", window.Name);
        Assert.Equal("left", window.Outline!.Id);
        Assert.False(window.CanRename);                  // the name is what ties the layout to the outline
        Assert.Equal(0, Element("left").ActiveLeds);
        Assert.False(Element("left").HasMarkup);
        Assert.Contains("no markup", Element("left").Description);

        window.Press(0, 0, GestureKeys.None);
        window.Release(0, 0);
        window.ApplyCommand.Execute(null);

        Assert.False(_editor.IsEditingMarkup);
        var layout = Assert.Single(_doc.Project.LedLayouts);
        Assert.Equal("left", layout.Name);
        Assert.Same(layout, Element("left").Layout);
        Assert.True(Element("left").HasMarkup);
        Assert.Equal(1, Element("left").ActiveLeds);
        Assert.Contains("LEDs: 1", Element("left").Description);
        Assert.Null(Element("right").Layout);
    }

    [Fact]
    public void Cancelling_the_window_leaves_the_outline_without_a_layout()
    {
        Import();
        Canvas.Edit(Element("left"));
        _editor.MarkupEditor!.CancelCommand.Execute(null);
        Assert.False(_editor.IsEditingMarkup);
        Assert.Empty(_doc.Project.LedLayouts);
    }

    [Fact]
    public void An_outline_that_has_a_layout_opens_it_and_the_inspector_of_a_video_timeline_opens_the_same_window_with_the_outline()
    {
        Import();
        Mark("left", (0, 0), (1, 0));
        var layout = _doc.Project.LedLayouts.Single();

        Canvas.Edit(Element("left"));
        Assert.Equal(2, _editor.MarkupEditor!.Markup.Count);
        _editor.MarkupEditor.CancelCommand.Execute(null);

        _editor.AddVideoTimelineCommand.Execute(null);
        var timeline = _doc.Project.Timelines[^1];
        _doc.Execute(Edits.SetLedLayout(_doc.Project, timeline, layout.Id));
        _editor.Timelines.Select(timeline);
        _editor.EditLayoutCommand.Execute(null);
        Assert.Equal("left", _editor.MarkupEditor!.Outline!.Id);
        _editor.MarkupEditor.CancelCommand.Execute(null);
    }

    [Fact]
    public void A_layout_of_its_own_has_no_outline()
    {
        Import();
        _editor.AddVideoTimelineCommand.Execute(null);
        _editor.Timelines.Select(_doc.Project.Timelines[^1]);
        _editor.NewLayoutCommand.Execute(null);
        Assert.Null(_editor.MarkupEditor!.Outline);
        Assert.True(_editor.MarkupEditor.CanRename);
        _editor.MarkupEditor.CancelCommand.Execute(null);
    }

    [Fact]
    public void Markup_is_copied_from_one_outline_and_pasted_on_another_with_its_grid_and_spacing()
    {
        Import();
        Canvas.Edit(Element("left"));
        var window = _editor.MarkupEditor!;
        window.ColumnsBox = 4; window.RowsBox = 3;
        window.ApplySizeCommand.Execute(null);
        window.FillAllCommand.Execute(null);
        window.Spacing = 6;
        window.ApplyCommand.Execute(null);

        Canvas.Selected = Element("right");
        Assert.False(Canvas.PasteMarkupCommand.CanExecute(null));        // nothing copied yet
        Canvas.Selected = Element("left");
        Assert.True(Canvas.CopyMarkupCommand.CanExecute(null));
        Canvas.CopyMarkupCommand.Execute(null);
        Assert.Contains("left", Canvas.Message);

        Canvas.Selected = Element("right");
        Assert.True(Canvas.PasteMarkupCommand.CanExecute(null));
        Canvas.PasteMarkupCommand.Execute(null);

        var left = Element("left").Layout!;
        var right = Element("right").Layout!;
        Assert.Equal("right", right.Name);
        Assert.NotEqual(left.Id, right.Id);
        Assert.NotSame(left.Leds, right.Leds);                            // a copy, not shared
        Assert.Equal((4, 3, 6), (right.Columns, right.Rows, right.TilesMargin));
        Assert.Equal(left.Leds, right.Leds);

        // Pasting over an outline that has markup replaces it in one step.
        Canvas.Selected = Element("left");
        Canvas.CopyMarkupCommand.Execute(null);
        Canvas.Selected = Element("right");
        right.Leds.RemoveRange(0, 0);
        _doc.Execute(Edits.SetLedMarkup(right, new LedMarkup(2, 2, [0]), 1));
        Canvas.PasteMarkupCommand.Execute(null);
        Assert.Equal(12, right.Leds.Count);
        _doc.Undo();
        Assert.Equal([0], right.Leds);
    }

    [Fact]
    public void An_outline_without_markup_cannot_be_copied()
    {
        Import();
        Canvas.Selected = Element("left");
        Assert.False(Canvas.CopyMarkupCommand.CanExecute(null));
        Canvas.Selected = null;
        Assert.False(Canvas.EditCommand.CanExecute(null));
    }

    [Fact]
    public void The_layout_of_an_outline_goes_to_the_selected_video_timeline()
    {
        Import();
        Mark("left", (0, 0));
        Canvas.Selected = Element("left");

        // No timeline selected: nothing happens, and the message says why.
        Canvas.UseForTimelineCommand.Execute(null);
        Assert.NotNull(Canvas.Message);

        _editor.AddVideoTimelineCommand.Execute(null);
        var timeline = _doc.Project.Timelines[^1];
        _editor.Timelines.Select(timeline);
        Canvas.UseForTimelineCommand.Execute(null);
        Assert.Equal(Element("left").Layout!.Id, timeline.LedLayoutId);
        Assert.Null(Canvas.Message);

        _doc.Undo();
        Assert.Null(timeline.LedLayoutId);
    }

    [Fact]
    public void An_outline_that_has_not_been_marked_up_cannot_be_given_to_a_timeline()
    {
        Import();
        Canvas.Selected = Element("right");
        Assert.False(Canvas.UseForTimelineCommand.CanExecute(null));
    }

    [Fact]
    public void The_markup_stays_with_its_outline_when_another_drawing_with_the_same_ids_is_imported()
    {
        Import();
        Mark("left", (0, 0));
        Import("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 50 50"><circle id="left" cx="25" cy="25" r="20"/></svg>""");
        Assert.True(Element("left").HasMarkup);
    }
}
