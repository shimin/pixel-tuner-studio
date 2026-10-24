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
    public void Each_kind_of_timeline_has_its_section_and_what_the_selected_timeline_cannot_play_is_dimmed_with_the_reason()
    {
        var pixel = Add(TimelineKind.Pixel);
        Image("tall.png", 10, 80);
        Image("short.png", 10, 36);
        _editor.Gallery.AddFile(Gif(), LibraryKind.Video);
        // A colour kept by an older build is not a section's business: the LightMaster has a fixed list.
        _doc.Execute(Edits.AddLibraryEntry(_doc.Project, new LibraryEntry { Name = "red", Kind = LibraryKind.Color, Color = "#FF0000" }));
        var gallery = _editor.Gallery;

        // No timeline selected: the section picked by hand lists its own kind, all of it.
        _editor.Timelines.Select(null);
        gallery.SectionIndex = 0;
        Assert.Equal(["short", "tall"], gallery.Entries.Select(e => e.Name).Order());
        gallery.SectionIndex = 1;
        Assert.Equal(["glasses"], gallery.Entries.Select(e => e.Name));

        // A pixel timeline that plays 36-LED pictures brings the pixel section forward; the tall picture stays, greyed.
        _doc.Execute(Edits.SetTimelineHeight(pixel, 36));
        _editor.Timelines.Select(pixel);
        Assert.Equal(0, gallery.SectionIndex);
        Assert.Equal(2, gallery.Entries.Count);
        var greyed = gallery.Entries.Single(e => e.Name == "tall");
        Assert.Equal((PlacementProblem.WrongHeight, false), (greyed.Problem, greyed.CanPlace));
        Assert.Contains("80", greyed.Reason);
        Assert.True(gallery.Entries.Single(e => e.Name == "short").CanPlace);

        // Another section shows its things too, with the reason they do not go on this timeline.
        gallery.SectionIndex = 1;
        Assert.Equal(PlacementProblem.WrongKind, gallery.Entries.Single().Problem);
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
    public void The_LightMaster_section_is_a_fixed_list_that_cannot_be_added_to_or_removed_from()
    {
        var light = Add(TimelineKind.Light);
        _editor.Timelines.Select(light);
        var gallery = _editor.Gallery;

        Assert.True(gallery.IsFixedSection);
        Assert.Equal(5, gallery.Entries.Count);
        Assert.Equal([LibraryKind.Color, LibraryKind.Effect, LibraryKind.Effect, LibraryKind.Effect, LibraryKind.Effect], gallery.Entries.Select(e => e.Entry.Kind));
        Assert.Equal([LightEffectKind.LinearTransition, LightEffectKind.SineWave, LightEffectKind.SquareWave, LightEffectKind.SawToothWave],
            gallery.Entries.Skip(1).Select(e => e.Entry.Effect));
        Assert.All(gallery.Entries, e => Assert.True(e.CanPlace && e.IsFixed));

        // Nothing of the list is in the project, and nothing of it can be removed or renamed.
        gallery.Selected = gallery.Entries[1];
        Assert.False(gallery.RemoveCommand.CanExecute(null));
        Assert.False(gallery.CanRename);
        gallery.SelectedName = "mine";
        Assert.Empty(_doc.Project.Library);
        Assert.NotEqual("mine", gallery.Entries[1].Name);

        // From the list onto the timeline: a colour as a solid item, an effect with its defaults.
        var solid = gallery.Place(gallery.Entries[0].Entry, light)!;
        Assert.Equal((ItemKind.Solid, "#FFFFFF"), (solid.Kind, solid.Color));
        var effect = gallery.Place(gallery.Entries[2].Entry, light)!;
        Assert.Equal((ItemKind.Effect, LightEffectKind.SineWave, "#000000", "#FFFFFF"), (effect.Kind, effect.Effect, effect.Color, effect.ColorLast));
        Assert.Empty(_doc.Project.Library);
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
        Assert.Equal(LibraryKind.Gradient, _editor.Gallery.Section.Kind);
        Assert.NotNull(_editor.Gallery.Place(entry, epic));
    }

    [Fact]
    public async Task Import_sorts_files_by_what_they_are_whatever_section_or_timeline_is_shown_and_selects_the_new_entry()
    {
        Add(TimelineKind.Light);   // selected: the LightMaster section is shown
        var gallery = _editor.Gallery;
        Assert.True(gallery.IsFixedSection);

        // A GIF is a video.
        _picker.ImagePath = Gif();
        await gallery.ImportCommand.ExecuteAsync(null);
        var video = Assert.Single(_doc.Project.Library);
        Assert.Equal(LibraryKind.Video, video.Kind);
        Assert.Equal(LibraryKind.Video, gallery.Section.Kind);
        Assert.Same(video, gallery.Selected!.Entry);
        Assert.Contains(gallery.Entries, e => ReferenceEquals(e.Entry, video));

        // A picture is a pixel picture, through the import panel.
        _picker.ImagePath = Picture("photo.png", 40, 20);
        await gallery.ImportCommand.ExecuteAsync(null);
        Assert.True(gallery.IsImporting);
        Assert.Equal(LibraryKind.Image, gallery.Section.Kind);
        gallery.Import!.ApplyCommand.Execute(null);
        var picture = _doc.Project.Library.Single(e => e.Kind == LibraryKind.Image);
        Assert.Same(picture, gallery.Selected!.Entry);

        // The same GIF again finds its entry and selects it.
        _picker.ImagePath = Gif();
        await gallery.ImportCommand.ExecuteAsync(null);
        Assert.Equal(2, _doc.Project.Library.Count);
        Assert.Equal(LibraryKind.Video, gallery.Section.Kind);
        Assert.Same(video, gallery.Selected!.Entry);
    }

    [Fact]
    public void The_pixel_section_can_be_narrowed_to_one_height_and_a_new_picture_is_never_hidden_by_it()
    {
        var gallery = _editor.Gallery;
        Image("a.png", 10, 36);
        Image("b.png", 10, 36);
        Image("c.png", 10, 72);
        Assert.True(gallery.HasHeightFilter);
        Assert.Equal(["All", "36", "72"], gallery.HeightFilterItems);
        Assert.Equal(3, gallery.Entries.Count);

        gallery.HeightFilterIndex = 1;
        Assert.Equal(["a", "b"], gallery.Entries.Select(e => e.Name).Order());
        gallery.HeightFilterIndex = 2;
        Assert.Equal(["c"], gallery.Entries.Select(e => e.Name));

        // A picture of another height is selected after import: the filter gives way so it shows.
        var tall = Image("d.png", 10, 80);
        Assert.Equal(0, gallery.HeightFilterIndex);
        Assert.Same(tall, gallery.Selected!.Entry);
        Assert.Equal(4, gallery.Entries.Count);

        // A height that no picture has any more is dropped from the filter.
        gallery.HeightFilterIndex = 3;   // 80, the tall picture only
        gallery.Selected = gallery.Entries.Single();
        gallery.RemoveCommand.Execute(null);
        Assert.Equal(["All", "36", "72"], gallery.HeightFilterItems);
        Assert.Equal(0, gallery.HeightFilterIndex);

        gallery.SectionIndex = 1;
        Assert.False(gallery.HasHeightFilter);
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

    // ---- the gate of the epic: nothing the panel puts on a timeline fails to build

    [Fact]
    public void Whatever_the_panel_puts_on_a_timeline_builds_and_the_rules_of_the_library_and_the_timeline_agree()
    {
        // Pictures of two heights, a gradient, a GIF, a colour and an effect; one timeline of every kind.
        var entries = new List<LibraryEntry>
        {
            Image("p36.png", 10, 36), Image("p80.png", 10, 80),
            _editor.Gallery.AddFile(Picture("grad.png", 20, 3), LibraryKind.Gradient),
            _editor.Gallery.AddFile(Gif(), LibraryKind.Video),
        };
        _doc.Execute(Edits.AddLibraryEntry(_doc.Project, new LibraryEntry { Name = "red", Kind = LibraryKind.Color, Color = "#FF0000" }));
        _doc.Execute(Edits.AddLibraryEntry(_doc.Project, new LibraryEntry
        {
            Name = "wave", Kind = LibraryKind.Effect, Color = "#000000", ColorLast = "#00FF00", Effect = LightEffectKind.SineWave, T1 = 300,
        }));
        entries.AddRange(_doc.Project.Library.Where(e => e.Kind is LibraryKind.Color or LibraryKind.Effect));

        var layout = new LedLayout { Name = "prop", Columns = 3, Rows = 3, Leds = [0, 1, 2, 3] };
        _doc.Project.LedLayouts.Add(layout);
        var timelines = new[] { TimelineKind.Pixel, TimelineKind.Video, TimelineKind.EpicPoi, TimelineKind.Light }.Select(Add).ToList();
        _doc.Execute(Edits.SetLedLayout(_doc.Project, timelines[1], layout.Id));
        string[] devices = [DeviceKind.Poi, DeviceKind.Video, DeviceKind.EpicPoi, DeviceKind.Light];

        for (int t = 0; t < timelines.Count; t++)
        {
            var timeline = timelines[t];
            foreach (var entry in entries)
            {
                bool allowed = Library.CanPlace(entry, timeline) == PlacementProblem.None;

                // The library and the timeline's own check of an item say the same.
                var probe = allowed ? Core.Model.Library.NewItem(entry, timeline) : null;
                if (probe is not null) Assert.True(Edits.Accepts(timeline, probe) && Edits.FitsHeight(timeline, probe.MediaHeight));

                _editor.Timelines.SetPlayhead(TimeSpan.FromSeconds(timeline.Items.Count * 6));
                var placed = _editor.Gallery.Place(entry, timeline);
                Assert.Equal(allowed, placed is not null);
            }

            var device = new DeviceBinding { DeviceType = devices[t], LedCount = devices[t] == DeviceKind.Poi ? Edits.PixelHeight(timeline) : 0, TimelineIds = [timeline.Id] };
            var result = new Core.Export.ShowBuilder(_doc.Project, new Core.Export.ShowMedia(id => _doc.OpenMedia(id)!)).Build(device);
            Assert.True(timeline.Items.Count > 0, $"nothing could be put on the {timeline.Kind} timeline");
            Assert.True(result.Succeeded, $"{timeline.Kind}: {string.Join(", ", result.Errors.Select(e => e.Code))}");
        }
    }
}
