using PixelTuner.Core.Editing;
using PixelTuner.Core.Export;
using PixelTuner.Core.Model;
using PixelTuner.Package;
using PixelTuner.UI.ViewModels;

namespace PixelTuner.UI.Tests;

public sealed class EditorMultiSelectionTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pt-multi").FullName;
    private readonly ProjectDocument _doc = ProjectDocument.CreateNew("Show");
    private readonly FakePicker _picker = new();
    private readonly EditorViewModel _editor;

    public EditorMultiSelectionTests() => _editor = new EditorViewModel(_doc, null, () => _picker);

    public void Dispose()
    {
        _editor.Dispose();
        Directory.Delete(_dir, true);
    }

    /// <summary>A 2 x 1 24-bit BMP.</summary>
    private string Bmp()
    {
        byte[] bmp =
        [
            0x42, 0x4D, 62, 0, 0, 0, 0, 0, 0, 0, 54, 0, 0, 0,
            40, 0, 0, 0, 2, 0, 0, 0, 1, 0, 0, 0, 1, 0, 24, 0, 0, 0, 0, 0, 8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 255, 255, 0, 0, 0, 0,
        ];
        string path = Path.Combine(_dir, "pic.bmp");
        File.WriteAllBytes(path, bmp);
        return path;
    }

    private async Task<Timeline> ThreePictures()
    {
        _editor.AddPixelTimelineCommand.Execute(null);
        var timeline = _doc.Project.Timelines[0];
        _picker.ImagePath = Bmp();
        foreach (double at in new[] { 0, 6, 12 })
        {
            _editor.Timelines.SetPlayhead(TimeSpan.FromSeconds(at));
            await _editor.AddItemCommand.ExecuteAsync(null);
        }
        return timeline;
    }

    private void SelectAll(Timeline timeline)
    {
        _editor.Timelines.Select(timeline.Items[0]);
        _editor.Timelines.ToggleItem(timeline.Items[1]);
        _editor.Timelines.ToggleItem(timeline.Items[2]);
    }

    private byte[] Bin() =>
        new ShowBuilder(_doc.Project, new ShowMedia(id => _doc.OpenMedia(id)!)).Build(DeviceKind.Poi, _doc.Project.Timelines, ledCount: 1).Bin!;

    [Fact]
    public async Task The_inspector_shows_how_many_items_are_selected_and_the_apply_button_needs_a_second_item()
    {
        var timeline = await ThreePictures();
        _editor.Timelines.Select(timeline.Items[0]);
        Assert.False(_editor.IsMultiSelection);
        Assert.False(_editor.ApplyToAllCommand.CanExecute(null));

        _editor.Timelines.ToggleItem(timeline.Items[1]);

        Assert.True(_editor.IsMultiSelection);
        Assert.Equal(2, _editor.SelectedItemCount);
        Assert.Contains("2", _editor.InspectorTitle);
        Assert.True(_editor.ApplyToAllCommand.CanExecute(null));
    }

    [Fact]
    public async Task Apply_to_all_gives_the_shown_items_settings_to_the_others_and_they_reach_the_bin()
    {
        var timeline = await ThreePictures();
        SelectAll(timeline);   // the last one picked is the one shown
        _editor.Stretching = 9;
        _editor.Brightness = 40;
        _editor.LaserOn = true;
        Assert.Equal(4, timeline.Items[0].Stretching);   // only the shown one changed so far

        _editor.ApplyToAllCommand.Execute(null);

        Assert.All(timeline.Items, i => Assert.Equal((9, (byte)40, true), (i.Stretching, i.Brightness, i.LaserOn)));
        var file = BinReader.ReadPpoi(Bin());
        var entries = file.Timelines[0].Entries.Where(e => e.Image.Width == 2).ToList();
        Assert.Equal(3, entries.Count);
        Assert.All(entries, e => Assert.Equal((9, true), ((int)e.Stretching, e.LaserOn)));
        Assert.Single(BinReader.Images(file), i => i.Width == 2);   // same file and brightness: one block
    }

    [Fact]
    public async Task One_undo_takes_the_whole_application_back_and_a_second_one_with_nothing_to_change_is_told_so()
    {
        var timeline = await ThreePictures();
        SelectAll(timeline);
        _editor.Stretching = 9;
        _editor.ApplyToAllCommand.Execute(null);
        Assert.Null(_editor.Message);

        _doc.Undo();   // the application
        Assert.All(timeline.Items.Take(2), i => Assert.Equal(4, i.Stretching));
        Assert.Equal(9, timeline.Items[2].Stretching);   // the shown item's own change is a step of its own

        _editor.ApplyToAllCommand.Execute(null);
        string? undoKey = _doc.UndoKey;
        _editor.ApplyToAllCommand.Execute(null);   // now all have it
        Assert.NotNull(_editor.Message);
        Assert.Equal(undoKey, _doc.UndoKey);       // and no empty step was added
    }

    [Fact]
    public async Task Delete_removes_all_the_selected_items_from_the_toolbar_too()
    {
        var timeline = await ThreePictures();
        SelectAll(timeline);

        _editor.DeleteSelectedCommand.Execute(null);

        Assert.Empty(timeline.Items);
        _doc.Undo();
        Assert.Equal(3, timeline.Items.Count);
    }
}
