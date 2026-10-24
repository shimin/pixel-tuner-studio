using PixelTuner.Core.Model;
using PixelTuner.Package;
using PixelTuner.UI.Timelines;
using PixelTuner.UI.ViewModels;

namespace PixelTuner.UI.Tests;

public class EditorViewModelTests
{
    private readonly ProjectDocument _doc = ProjectDocument.CreateNew("Show");
    private readonly EditorViewModel _editor;

    public EditorViewModelTests() => _editor = new EditorViewModel(_doc);

    [Fact]
    public void Adding_timelines_names_them_and_selects_the_new_one()
    {
        _editor.AddPixelTimelineCommand.Execute(null);
        _editor.AddVideoTimelineCommand.Execute(null);
        _editor.AddEpicTimelineCommand.Execute(null);
        _editor.AddLightTimelineCommand.Execute(null);

        Assert.Equal(["Timeline 1", "Timeline 2", "Timeline 3", "Timeline 4"], _doc.Project.Timelines.Select(t => t.Name));
        Assert.Equal([TimelineKind.Pixel, TimelineKind.Video, TimelineKind.EpicPoi, TimelineKind.Light],
            _doc.Project.Timelines.Select(t => t.Kind));
        Assert.Same(_doc.Project.Timelines[3], _editor.Timelines.Selected);
        Assert.True(_doc.IsDirty);
    }

    [Fact]
    public void The_inspector_follows_the_selection_and_renames_through_undo()
    {
        Assert.False(_editor.HasSelection);
        Assert.False(_editor.DeleteSelectedCommand.CanExecute(null));

        _editor.AddPixelTimelineCommand.Execute(null);
        Assert.True(_editor.HasSelection);
        Assert.True(_editor.CanRename);
        Assert.Equal("Timeline", _editor.InspectorTitle);
        Assert.True(_editor.DeleteSelectedCommand.CanExecute(null));

        _editor.SelectionName = "Poi left";
        Assert.Equal("Poi left", _doc.Project.Timelines[0].Name);

        _doc.Undo();
        Assert.Equal("Timeline 1", _editor.SelectionName);
    }

    [Fact]
    public void Markers_are_added_at_the_playhead_and_renamed()
    {
        _editor.Timelines.SetPlayhead(TimeSpan.FromSeconds(4));
        _editor.AddMarkerCommand.Execute(null);

        var marker = Assert.Single(_doc.Project.Markers);
        Assert.Equal(TimeSpan.FromSeconds(4), marker.Position);
        Assert.Equal("Marker 1", marker.Name);
        Assert.True(_editor.IsMarkerSelected);
        Assert.Equal("0:04.000", _editor.PositionText);

        _editor.SelectionName = "Drop";
        Assert.Equal("Drop", marker.Name);
    }

    [Fact]
    public void An_item_shows_its_times_and_cannot_be_renamed()
    {
        _editor.AddPixelTimelineCommand.Execute(null);
        var item = new TimelineItem { Start = TimeSpan.FromSeconds(1.25), Duration = TimeSpan.FromSeconds(2) };
        _doc.Project.Timelines[0].Items.Add(item);
        _editor.Timelines.Select(item);

        Assert.True(_editor.IsItemSelected);
        Assert.False(_editor.CanRename);
        Assert.Equal("0:01.250", _editor.StartText);
        Assert.Equal("0:02.000", _editor.LengthText);
    }

    [Fact]
    public void Delete_removes_the_selection()
    {
        _editor.AddPixelTimelineCommand.Execute(null);
        _editor.DeleteSelectedCommand.Execute(null);

        Assert.Empty(_doc.Project.Timelines);
        Assert.False(_editor.HasSelection);
        _doc.Undo();
        Assert.Single(_doc.Project.Timelines);
    }

    [Fact]
    public void Zoom_commands_change_the_scale_and_fit_shows_the_show()
    {
        double before = _editor.Timelines.PixelsPerSecond;
        _editor.ZoomInCommand.Execute(null);
        Assert.True(_editor.Timelines.PixelsPerSecond > before);
        _editor.ZoomOutCommand.Execute(null);
        Assert.Equal(before, _editor.Timelines.PixelsPerSecond, 6);
        _editor.ZoomFitCommand.Execute(null);
        Assert.NotEqual(before, _editor.Timelines.PixelsPerSecond);
    }

    [Fact]
    public void The_inspector_can_be_folded_away_and_brought_back()
    {
        Assert.True(_editor.IsInspectorVisible);
        Assert.Equal("›", _editor.InspectorToggleGlyph);
        var changed = new List<string?>();
        _editor.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _editor.ToggleInspectorCommand.Execute(null);
        Assert.False(_editor.IsInspectorVisible);
        Assert.Equal("‹", _editor.InspectorToggleGlyph);
        Assert.Contains(nameof(EditorViewModel.InspectorToggleTip), changed);

        _editor.ToggleInspectorCommand.Execute(null);
        Assert.True(_editor.IsInspectorVisible);
    }

    [Fact]
    public void The_bottom_panel_can_be_folded_away_and_brought_back()
    {
        Assert.True(_editor.IsBottomPanelVisible);
        Assert.Equal("▾", _editor.BottomPanelToggleGlyph);

        _editor.ToggleBottomPanelCommand.Execute(null);
        Assert.False(_editor.IsBottomPanelVisible);
        Assert.Equal("▴", _editor.BottomPanelToggleGlyph);

        _editor.ToggleBottomPanelCommand.Execute(null);
        Assert.True(_editor.IsBottomPanelVisible);
    }

    [Fact]
    public void The_zoom_slider_sets_the_scale_and_follows_the_wheel_and_fit()
    {
        // 0 is the widest view, 1 the closest.
        _editor.ZoomSlider = 0;
        Assert.Equal(TimelineEditor.MinZoom, _editor.Timelines.PixelsPerSecond, 6);
        _editor.ZoomSlider = 1;
        Assert.Equal(TimelineEditor.MaxZoom, _editor.Timelines.PixelsPerSecond, 6);

        _editor.ZoomSlider = 0.5;
        Assert.Equal(Math.Sqrt(TimelineEditor.MinZoom * TimelineEditor.MaxZoom), _editor.Timelines.PixelsPerSecond, 6);

        // What the wheel does moves the slider, and the slider echoing it back changes nothing.
        var seen = new List<string?>();
        _editor.PropertyChanged += (_, e) => seen.Add(e.PropertyName);
        _editor.Timelines.ZoomAt(2, TimelineEditor.HeaderWidth);
        Assert.Contains(nameof(EditorViewModel.ZoomSlider), seen);
        double scale = _editor.Timelines.PixelsPerSecond;
        _editor.ZoomSlider = _editor.ZoomSlider;
        Assert.Equal(scale, _editor.Timelines.PixelsPerSecond, 9);
    }
}

public class EditorOwnershipTests
{
    [Fact]
    public async Task Studio_gets_an_editor_for_the_open_project_and_loses_it_on_close()
    {
        using var vm = new MainViewModel { FilePicker = new FakePicker(), Dialogs = new FakeDialogs(PixelTuner.UI.Services.SaveChoice.Discard) };
        Assert.False(vm.ShowEditor);
        Assert.True(vm.ShowStart);

        await vm.NewProjectCommand.ExecuteAsync(null);
        Assert.True(vm.ShowEditor);
        Assert.False(vm.ShowStart);
        Assert.Same(vm.Document, vm.Editor!.Document);

        await vm.CloseProjectCommand.ExecuteAsync(null);
        Assert.Null(vm.Editor);
        Assert.True(vm.ShowStart);
    }

    [Fact]
    public async Task A_new_project_starts_with_the_human_schema_as_PixelTuner_2_did_and_is_still_clean()
    {
        using var vm = new MainViewModel { FilePicker = new FakePicker(), Dialogs = new FakeDialogs(PixelTuner.UI.Services.SaveChoice.Discard) };
        await vm.NewProjectCommand.ExecuteAsync(null);

        Assert.False(vm.Document!.IsDirty);                       // it is the starting state, not an edit
        Assert.NotNull(vm.Document.Project.CanvasResourceId);
        Assert.Equal(12, vm.Editor!.Canvas.Elements.Count);
        Assert.False(vm.Document.CanUndo);
    }

    [Fact]
    public async Task Stage_never_gets_an_editor()
    {
        var previous = Product.Current;
        Product.Current = Product.Stage;
        try
        {
            string path = Path.Combine(Directory.CreateTempSubdirectory("stage").FullName, "s.pxpkg");
            new PxPackageWriter().Save(path, new ShowProject { Name = "S" });
            using var vm = new MainViewModel { FilePicker = new FakePicker { OpenStream = new MemoryStream(File.ReadAllBytes(path)) } };

            await vm.OpenProjectCommand.ExecuteAsync(null);

            Assert.True(vm.HasProject);
            Assert.Null(vm.Editor);
            Assert.True(vm.ShowStart);
        }
        finally
        {
            Product.Current = previous;
        }
    }
}
