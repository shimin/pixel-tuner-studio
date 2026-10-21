using PixelTuner.Core.Editing;
using PixelTuner.Core.Model;
using PixelTuner.Package;
using PixelTuner.UI.ViewModels;
using SkiaSharp;

namespace PixelTuner.UI.Tests;

public sealed class LibraryViewModelTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pt-library").FullName;
    private readonly ProjectDocument _doc = ProjectDocument.CreateNew("Show");
    private readonly FakePicker _picker = new();
    private readonly EditorViewModel _editor;

    public LibraryViewModelTests() => _editor = new EditorViewModel(_doc, null, () => _picker);

    public void Dispose()
    {
        _editor.Dispose();
        Directory.Delete(_dir, true);
    }

    private int _shade;

    // Every picture gets its own colour: the package stores equal files once, and equal files are one entry.
    private string Picture(string name, int width, int height, SKColor? color = null)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color ?? new SKColor((byte)(20 + 10 * ++_shade), 100, 50));
        using var image = SKImage.FromBitmap(bitmap);
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, image.Encode(SKEncodedImageFormat.Png, 100).ToArray());
        return path;
    }

    private static string Gif() => Path.Combine(AppContext.BaseDirectory, "Fixtures", "glasses", "glasses.gif");

    private Timeline Add(string kind)
    {
        switch (kind)
        {
            case TimelineKind.Pixel: _editor.AddPixelTimelineCommand.Execute(null); break;
            case TimelineKind.Video: _editor.AddVideoTimelineCommand.Execute(null); break;
            case TimelineKind.EpicPoi: _editor.AddEpicTimelineCommand.Execute(null); break;
            default: _editor.AddLightTimelineCommand.Execute(null); break;
        }
        return _doc.Project.Timelines[^1];
    }

    private LibraryEntry Image(string name, int width, int height)
    {
        var entry = _editor.Gallery.AddFile(Picture(name, width, height), LibraryKind.Image);
        return entry;
    }

    [Fact]
    public void A_gif_goes_into_the_library_with_its_frames_and_a_video_timeline_takes_it_with_40_ms()
    {
        var timeline = Add(TimelineKind.Video);
        var entry = _editor.Gallery.AddFile(Gif(), LibraryKind.Video);

        Assert.Equal((LibraryKind.Video, "glasses"), (entry.Kind, entry.Name));
        Assert.True(entry.Frames > 1);
        Assert.Contains(entry.Frames.ToString(), _editor.Gallery.Entries.Single().Detail);

        var item = _editor.Gallery.Place(entry, timeline)!;
        Assert.Equal((ItemKind.Video, 40, entry.ResourceId), (item.Kind, item.Stretching, item.ResourceId));
    }

    [Fact]
    public void The_same_file_twice_is_one_entry()
    {
        string path = Picture("a.png", 8, 8);
        var first = _editor.Gallery.AddFile(path, LibraryKind.Image);
        var second = _editor.Gallery.AddFile(path, LibraryKind.Image);

        Assert.Same(first, second);
        Assert.Single(_doc.Project.Library);
    }

    [Fact]
    public void The_panel_offers_only_what_the_selected_timeline_can_play()
    {
        var pixel = Add(TimelineKind.Pixel);
        var tall = Image("tall.png", 10, 80);
        var short1 = Image("short.png", 10, 36);
        _editor.Gallery.AddFile(Gif(), LibraryKind.Video);
        _doc.Execute(Edits.AddLibraryEntry(_doc.Project, new LibraryEntry { Name = "red", Kind = LibraryKind.Color, Color = "#FF0000" }));

        // No timeline selected: everything.
        _editor.Timelines.Select(null);
        Assert.Equal(4, _editor.Gallery.Entries.Count);

        // A pixel timeline that plays 36-LED pictures: only those.
        _doc.Execute(Edits.SetTimelineHeight(pixel, 36));
        _editor.Timelines.Select(pixel);
        Assert.Equal(["short"], _editor.Gallery.Entries.Select(e => e.Name));
        Assert.True(_editor.Gallery.IsFiltered);

        // "Show everything" lists the rest greyed, with the reason.
        _editor.Gallery.ShowAll = true;
        Assert.Equal(4, _editor.Gallery.Entries.Count);
        var greyed = _editor.Gallery.Entries.Single(e => e.Name == "tall");
        Assert.Equal((PlacementProblem.WrongHeight, false), (greyed.Problem, greyed.CanPlace));
        Assert.Contains("80", greyed.Reason);
        Assert.Equal(PlacementProblem.WrongKind, _editor.Gallery.Entries.Single(e => e.Name == "red").Problem);
        Assert.True(_editor.Gallery.Entries.Single(e => e.Name == "short").CanPlace);
        Assert.NotNull(short1);
        Assert.NotNull(tall);
    }

    [Fact]
    public void Placing_puts_the_item_at_the_playhead_and_a_greyed_entry_goes_nowhere()
    {
        var pixel = Add(TimelineKind.Pixel);
        var ok = Image("a.png", 10, 36);
        var other = Image("b.png", 10, 80);
        _editor.Timelines.Select(pixel);
        _editor.Timelines.SetPlayhead(TimeSpan.FromSeconds(2));
        _editor.Gallery.Selected = _editor.Gallery.Entries.Single(e => e.Name == "a");

        _editor.Gallery.PlaceCommand.Execute(null);
        var item = Assert.Single(pixel.Items);
        Assert.Equal((TimeSpan.FromSeconds(2), 4, ok.ResourceId), (item.Start, item.Stretching, item.ResourceId));
        Assert.Equal(36, item.MediaHeight);

        // The height is fixed by the first picture now: the 80 LED picture is not offered, and a direct try does nothing.
        Assert.Null(_editor.Gallery.Place(other, pixel));
        Assert.Single(pixel.Items);
    }

    [Fact]
    public void Placing_with_no_room_says_so()
    {
        var pixel = Add(TimelineKind.Pixel);
        var entry = Image("a.png", 10, 36);
        _editor.Gallery.Place(entry, pixel);
        _editor.Timelines.SetPlayhead(TimeSpan.FromSeconds(1));   // inside the item that is there: the next free time is after it
        Assert.NotNull(_editor.Gallery.Place(entry, pixel));
        Assert.Equal(2, pixel.Items.Count);
        Assert.Null(_editor.Gallery.Message);
    }

    [Fact]
    public void An_entry_an_item_plays_cannot_be_removed_and_a_free_one_can_with_undo()
    {
        var pixel = Add(TimelineKind.Pixel);
        var used = Image("used.png", 10, 36);
        var free = Image("free.png", 10, 36);
        _editor.Gallery.Place(used, pixel);

        _editor.Gallery.Selected = _editor.Gallery.Entries.Single(e => e.Name == "used");
        _editor.Gallery.RemoveCommand.Execute(null);
        Assert.Contains(used, _doc.Project.Library);
        Assert.NotNull(_editor.Gallery.Message);

        _editor.Gallery.Selected = _editor.Gallery.Entries.Single(e => e.Name == "free");
        _editor.Gallery.RemoveCommand.Execute(null);
        Assert.DoesNotContain(free, _doc.Project.Library);
        _doc.Undo();
        Assert.Contains(free, _doc.Project.Library);
    }

    [Fact]
    public void An_entry_is_renamed_from_the_panel()
    {
        var entry = Image("a.png", 10, 36);
        _editor.Gallery.Selected = _editor.Gallery.Entries.Single();
        _editor.Gallery.SelectedName = "Flag";
        Assert.Equal("Flag", entry.Name);
        _doc.Undo();
        Assert.Equal("a", entry.Name);
    }

    [Fact]
    public void Colours_and_effects_are_added_and_kept_from_light_items()
    {
        var light = Add(TimelineKind.Light);
        _editor.Gallery.NewColor = "#336699";
        _editor.Gallery.AddColorCommand.Execute(null);
        Assert.Equal("#336699", _doc.Project.Library.Single().Color);

        _editor.Gallery.NewColor = "blue-ish";
        _editor.Gallery.AddColorCommand.Execute(null);
        Assert.Single(_doc.Project.Library);
        Assert.NotNull(_editor.Gallery.Message);

        // An effect set up on the timeline is kept for later.
        _editor.AddEffectCommand.Execute(null);
        _editor.Gallery.SaveSelectedItemCommand.Execute(null);
        var effect = _doc.Project.Library.Single(e => e.Kind == LibraryKind.Effect);
        Assert.Equal((LightEffectKind.LinearTransition, "#FFFFFF"), (effect.Effect, effect.ColorLast));

        // From the library onto the timeline again, as the same kind of item.
        var placed = _editor.Gallery.Place(effect, light)!;
        Assert.Equal((ItemKind.Effect, LightEffectKind.LinearTransition), (placed.Kind, placed.Effect));
    }

    [Fact]
    public async Task Importing_for_a_pixel_timeline_opens_the_panel_scaled_to_its_height_and_the_result_lands_in_the_library()
    {
        var pixel = Add(TimelineKind.Pixel);
        _doc.Execute(Edits.SetTimelineHeight(pixel, 36));
        _editor.Timelines.Select(pixel);
        _picker.ImagePath = Picture("photo.png", 200, 100);

        await _editor.Gallery.ImportCommand.ExecuteAsync(null);

        var import = _editor.Gallery.Import!;
        Assert.True(_editor.Gallery.IsImporting);
        Assert.True(import.HeightIsFixed);
        Assert.Equal((72, 36), (import.ResultWidth, import.ResultHeight));

        import.ApplyCommand.Execute(null);
        Assert.False(_editor.Gallery.IsImporting);
        var entry = Assert.Single(_doc.Project.Library);
        Assert.Equal((72, 36), (entry.Width, entry.Height));
        Assert.Same(entry, _editor.Gallery.Selected!.Entry);
        Assert.True(_editor.Gallery.PlaceCommand.CanExecute(null));
    }

    [Fact]
    public async Task Importing_for_an_epic_timeline_takes_a_gradient_as_it_is()
    {
        var epic = Add(TimelineKind.EpicPoi);
        _editor.Timelines.Select(epic);
        _picker.ImagePath = Picture("grad.png", 60, 4);

        await _editor.Gallery.ImportCommand.ExecuteAsync(null);

        Assert.False(_editor.Gallery.IsImporting);
        var entry = Assert.Single(_doc.Project.Library);
        Assert.Equal((LibraryKind.Gradient, 60), (entry.Kind, entry.Width));
        Assert.NotNull(_editor.Gallery.Place(entry, epic));
    }

    [Fact]
    public async Task A_light_timeline_imports_nothing()
    {
        var light = Add(TimelineKind.Light);
        _editor.Timelines.Select(light);
        _picker.ImagePath = Picture("a.png", 4, 4);

        await _editor.Gallery.ImportCommand.ExecuteAsync(null);

        Assert.Empty(_doc.Project.Library);
        Assert.False(_editor.Gallery.IsImporting);
    }

    [Fact]
    public void A_missing_file_is_listed_apart_and_a_found_file_fixes_its_items()
    {
        var pixel = Add(TimelineKind.Pixel);
        pixel.Items.Add(new TimelineItem { ResourceId = null, SourcePath = ".\\Gone.png", Duration = TimeSpan.FromSeconds(1) });
        Library_AddMissing();

        var missing = Assert.Single(_editor.Gallery.Missing);
        Assert.True(missing.IsMissing);

        string found = Picture("Gone.png", 6, 36);
        _editor.Gallery.ReplaceFile(missing.Entry, found);

        Assert.Empty(_editor.Gallery.Missing);
        Assert.NotNull(pixel.Items[0].ResourceId);
        Assert.Equal(36, pixel.Items[0].MediaHeight);
        Assert.Equal(pixel.Items[0].ResourceId, _doc.Project.Library.Single().ResourceId);
    }

    private void Library_AddMissing()
    {
        Core.Model.Library.AddMissingEntries(_doc.Project);
        _editor.Gallery.Refresh();
    }

    [Fact]
    public async Task Finding_a_file_of_the_wrong_height_for_a_missing_one_is_refused_with_a_reason()
    {
        var pixel = Add(TimelineKind.Pixel);
        pixel.Items.Add(new TimelineItem { ResourceId = "x", SourcePath = "a.png", MediaWidth = 3, MediaHeight = 36, Start = TimeSpan.Zero, Duration = TimeSpan.FromSeconds(1) });
        pixel.Items.Add(new TimelineItem { ResourceId = null, SourcePath = "gone.png", Start = TimeSpan.FromSeconds(2), Duration = TimeSpan.FromSeconds(1) });
        Library_AddMissing();

        _picker.ImagePath = Picture("tall.png", 5, 80);
        await _editor.Gallery.ReplaceCommand.ExecuteAsync(_editor.Gallery.Missing.Single());

        Assert.NotNull(_editor.Gallery.Message);
        Assert.Single(_editor.Gallery.Missing);   // still missing
        Assert.Null(pixel.Items[1].ResourceId);
    }
}
