using PixelTuner.Core.Editing;
using PixelTuner.Core.Export;
using PixelTuner.Core.Model;
using SkiaSharp;

namespace PixelTuner.Core.Tests;

public class LibraryTests
{
    private readonly ShowProject _project = new();
    private readonly UndoHistory _history = new();
    private readonly Dictionary<string, byte[]> _files = [];

    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    private LibraryEntry Picture(string name, int width, int height, string kind = LibraryKind.Image)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(new SKColor(200, 100, 50));
        using var image = SKImage.FromBitmap(bitmap);
        _files[name] = image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
        return new LibraryEntry { Name = name, Kind = kind, ResourceId = name, SourcePath = name, Width = width, Height = height };
    }

    private Timeline Timeline(string kind, int height = 0)
    {
        var timeline = new Timeline { Kind = kind, Height = height };
        _history.Do(Edits.AddTimeline(_project, timeline));
        return timeline;
    }

    private TimelineItem Place(LibraryEntry entry, Timeline timeline, double start = 0)
    {
        var item = Library.NewItem(entry, timeline);
        item.Start = S(start);
        item.Duration = S(1);
        _history.Do(Edits.AddItem(timeline, item));
        return item;
    }

    // ---- what goes where

    [Theory]
    [InlineData(LibraryKind.Image, TimelineKind.Pixel, PlacementProblem.None)]
    [InlineData(LibraryKind.Image, TimelineKind.Video, PlacementProblem.WrongKind)]
    [InlineData(LibraryKind.Image, TimelineKind.EpicPoi, PlacementProblem.WrongKind)]
    [InlineData(LibraryKind.Image, TimelineKind.Light, PlacementProblem.WrongKind)]
    [InlineData(LibraryKind.Video, TimelineKind.Video, PlacementProblem.None)]
    [InlineData(LibraryKind.Video, TimelineKind.Pixel, PlacementProblem.WrongKind)]
    [InlineData(LibraryKind.Gradient, TimelineKind.EpicPoi, PlacementProblem.None)]
    [InlineData(LibraryKind.Gradient, TimelineKind.Pixel, PlacementProblem.WrongKind)]
    [InlineData(LibraryKind.Color, TimelineKind.Light, PlacementProblem.None)]
    [InlineData(LibraryKind.Effect, TimelineKind.Light, PlacementProblem.None)]
    [InlineData(LibraryKind.Color, TimelineKind.Pixel, PlacementProblem.WrongKind)]
    public void An_entry_goes_on_the_kind_of_timeline_that_plays_it(string entryKind, string timelineKind, PlacementProblem expected)
    {
        var entry = new LibraryEntry { Kind = entryKind, ResourceId = "x", Width = 4, Height = 4, Color = "#FFFFFF" };
        Assert.Equal(expected, Library.CanPlace(entry, new Timeline { Kind = timelineKind }));
    }

    [Fact]
    public void A_picture_goes_only_on_a_pixel_timeline_of_its_height_and_a_missing_file_goes_nowhere()
    {
        var picture = Picture("a", 8, 36);
        Assert.Equal(PlacementProblem.None, Library.CanPlace(picture, new Timeline()));
        Assert.Equal(PlacementProblem.None, Library.CanPlace(picture, new Timeline { Height = 36 }));
        Assert.Equal(PlacementProblem.WrongHeight, Library.CanPlace(picture, new Timeline { Height = 80 }));

        var other = Timeline(TimelineKind.Pixel);
        _history.Do(Edits.AddItem(other, new TimelineItem { MediaHeight = 80, Duration = S(1) }));
        Assert.Equal(PlacementProblem.WrongHeight, Library.CanPlace(picture, other));   // the first picture sets it

        var missing = new LibraryEntry { Kind = LibraryKind.Image, SourcePath = "gone.png" };
        Assert.Equal(PlacementProblem.MissingMedia, Library.CanPlace(missing, new Timeline()));
    }

    [Fact]
    public void A_new_item_gets_the_defaults_of_its_kind()
    {
        var picture = Picture("a", 8, 36);
        var item = Library.NewItem(picture, new Timeline());
        Assert.Equal((ItemKind.Image, "a", 8, 36, 4, 100), (item.Kind, item.ResourceId, item.MediaWidth, item.MediaHeight, item.Stretching, (int)item.Brightness));

        var video = Library.NewItem(new LibraryEntry { Kind = LibraryKind.Video, ResourceId = "g", SourcePath = "g.gif", Width = 20, Height = 10, Frames = 9 }, new Timeline { Kind = TimelineKind.Video });
        Assert.Equal((ItemKind.Video, 40), (video.Kind, video.Stretching));   // 40 ms per frame: 25 a second

        var gradient = Library.NewItem(new LibraryEntry { Kind = LibraryKind.Gradient, ResourceId = "g", Width = 30, Height = 1 }, new Timeline { Kind = TimelineKind.EpicPoi });
        Assert.Equal((ItemKind.Gradient, 4), (gradient.Kind, gradient.Stretching));

        var effect = Library.NewItem(
            new LibraryEntry { Kind = LibraryKind.Effect, Color = "#000000", ColorLast = "#FF0000", Effect = LightEffectKind.SineWave, T1 = 500, T2 = 100 },
            new Timeline { Kind = TimelineKind.Light });
        Assert.Equal((ItemKind.Effect, LightEffectKind.SineWave, 500, 100, "#FF0000"), (effect.Kind, effect.Effect, effect.T1, effect.T2, effect.ColorLast));

        var colour = Library.NewItem(new LibraryEntry { Kind = LibraryKind.Color, Color = "#102030" }, new Timeline { Kind = TimelineKind.Light });
        Assert.Equal((ItemKind.Solid, "#102030"), (colour.Kind, colour.Color));

        Assert.Throws<InvalidOperationException>(() => Library.NewItem(picture, new Timeline { Kind = TimelineKind.Light }));
    }

    // ---- the library of a project

    [Fact]
    public void A_project_from_before_the_library_gets_an_entry_for_each_file_its_items_play()
    {
        var timeline = Timeline(TimelineKind.Pixel);
        _history.Do(Edits.AddItem(timeline, new TimelineItem { ResourceId = "a", SourcePath = "C:\\show\\A.bmp", MediaWidth = 5, MediaHeight = 36, Start = S(0), Duration = S(1) }));
        _history.Do(Edits.AddItem(timeline, new TimelineItem { ResourceId = "a", SourcePath = "C:\\show\\A.bmp", MediaWidth = 5, MediaHeight = 36, Start = S(2), Duration = S(1) }));
        _history.Do(Edits.AddItem(timeline, new TimelineItem { ResourceId = null, SourcePath = "./Gone.bmp", Start = S(4), Duration = S(1) }));
        _history.Do(Edits.AddItem(timeline, new TimelineItem { ResourceId = null, SourcePath = "./gone.BMP", Start = S(6), Duration = S(1) }));
        var light = Timeline(TimelineKind.Light);
        _history.Do(Edits.AddItem(light, new TimelineItem { Kind = ItemKind.Solid, Color = "#FFFFFF", Duration = S(1) }));

        int added = Library.AddMissingEntries(_project);

        Assert.Equal(2, added);   // one file, one missing file told by its path in any case; a colour is not media
        Assert.Equal(["A.bmp", "Gone.bmp"], _project.Library.Select(e => e.Name));
        Assert.Equal((LibraryKind.Image, 5, 36), (_project.Library[0].Kind, _project.Library[0].Width, _project.Library[0].Height));
        Assert.Null(_project.Library[1].ResourceId);
        Assert.Equal(0, Library.AddMissingEntries(_project));   // nothing twice
    }

    [Fact]
    public void Entries_are_added_renamed_and_removed_with_undo()
    {
        var entry = Picture("a", 8, 36);
        _history.Do(Edits.AddLibraryEntry(_project, entry));
        _history.Do(Edits.RenameLibraryEntry(entry, "Flag"));
        Assert.Equal("Flag", _project.Library[0].Name);

        _history.Do(Edits.RemoveLibraryEntry(_project, entry));
        Assert.Empty(_project.Library);
        _history.Undo();
        _history.Undo();
        _history.Undo();
        Assert.Empty(_project.Library);
        Assert.Throws<ArgumentException>(() => { _history.Do(Edits.AddLibraryEntry(_project, entry)); Edits.AddLibraryEntry(_project, entry); });
    }

    [Fact]
    public void An_entry_that_an_item_plays_cannot_be_removed()
    {
        var entry = Picture("a", 8, 36);
        _history.Do(Edits.AddLibraryEntry(_project, entry));
        var timeline = Timeline(TimelineKind.Pixel);
        Place(entry, timeline);

        Assert.Throws<InvalidOperationException>(() => Edits.RemoveLibraryEntry(_project, entry));

        _history.Do(Edits.RemoveItem(timeline, timeline.Items[0]));
        _history.Do(Edits.RemoveLibraryEntry(_project, entry));   // free now
    }

    [Fact]
    public void Colours_and_effects_must_be_valid_to_enter_the_library()
    {
        Assert.Throws<InvalidDataException>(() => Edits.AddLibraryEntry(_project, new LibraryEntry { Kind = LibraryKind.Color, Color = "green" }));
        Assert.Throws<ArgumentException>(() => Edits.AddLibraryEntry(_project,
            new LibraryEntry { Kind = LibraryKind.Effect, Color = "#000000", ColorLast = "#FFFFFF", Effect = "disco" }));
        Assert.Throws<ArgumentException>(() => Edits.AddLibraryEntry(_project, new LibraryEntry { Kind = "sound" }));
        Assert.Throws<ArgumentException>(() => Edits.AddLibraryEntry(_project, new LibraryEntry { Kind = LibraryKind.Image, ResourceId = "x" }));   // no size
        _history.Do(Edits.AddLibraryEntry(_project, new LibraryEntry { Kind = LibraryKind.Image, SourcePath = "missing.png" }));   // a missing file has none yet
    }

    // ---- replacing a file

    [Fact]
    public void A_replaced_file_is_the_picture_of_the_entry_and_of_every_item_that_played_the_old_one()
    {
        var entry = Picture("old", 8, 36);
        _history.Do(Edits.AddLibraryEntry(_project, entry));
        var one = Timeline(TimelineKind.Pixel);
        var two = Timeline(TimelineKind.Pixel);
        Place(entry, one);
        Place(entry, two);

        _history.Do(Edits.ReplaceLibraryMedia(_project, entry, "new", "new.png", 12, 36));

        Assert.Equal(("new", 12), (entry.ResourceId, entry.Width));
        Assert.All(one.Items.Concat(two.Items), i => Assert.Equal(("new", "new.png", 12, 36), (i.ResourceId, i.SourcePath, i.MediaWidth, i.MediaHeight)));
        _history.Undo();
        Assert.All(one.Items.Concat(two.Items), i => Assert.Equal(("old", 8), (i.ResourceId, i.MediaWidth)));
    }

    [Fact]
    public void A_file_found_for_a_missing_one_fills_the_entry_and_its_items()
    {
        var timeline = Timeline(TimelineKind.Pixel);
        _history.Do(Edits.AddItem(timeline, new TimelineItem { ResourceId = null, SourcePath = ".\\Gone.bmp", Duration = S(1) }));
        Library.AddMissingEntries(_project);

        _history.Do(Edits.ReplaceLibraryMedia(_project, _project.Library[0], "found", "C:\\x\\Gone.bmp", 6, 36));

        Assert.Equal(("found", 36), (timeline.Items[0].ResourceId, timeline.Items[0].MediaHeight));
        Assert.Equal("found", _project.Library[0].ResourceId);
    }

    [Fact]
    public void A_replacement_of_another_height_is_refused_where_it_would_mix_heights()
    {
        var entry = Picture("a", 8, 36);
        _history.Do(Edits.AddLibraryEntry(_project, entry));
        var timeline = Timeline(TimelineKind.Pixel);
        Place(entry, timeline);
        _history.Do(Edits.AddItem(timeline, new TimelineItem { ResourceId = "other", MediaWidth = 3, MediaHeight = 36, Start = S(2), Duration = S(1) }));

        Assert.Throws<InvalidOperationException>(() => Edits.ReplaceLibraryMedia(_project, entry, "tall", "tall.png", 8, 80));
        Assert.Equal("a", timeline.Items[0].ResourceId);
        Assert.Throws<InvalidOperationException>(() => Edits.ReplaceLibraryMedia(_project, new LibraryEntry { Kind = LibraryKind.Color, Color = "#FFFFFF" }, "x", "x", 1, 1));
    }

    // ---- the gate: what the library offers can be built

    [Theory]
    [InlineData(TimelineKind.Pixel, DeviceKind.Poi, 36)]
    [InlineData(TimelineKind.Pixel, DeviceKind.Fan, 0)]
    [InlineData(TimelineKind.EpicPoi, DeviceKind.EpicPoi, 0)]
    [InlineData(TimelineKind.Light, DeviceKind.Light, 0)]
    public void Whatever_the_library_lets_onto_a_timeline_builds_into_a_bin(string timelineKind, string deviceKind, int leds)
    {
        var entries = new List<LibraryEntry>
        {
            Picture("p36", 8, 36), Picture("p80", 8, 80), Picture("g", 12, 1, LibraryKind.Gradient),
            new() { Name = "red", Kind = LibraryKind.Color, Color = "#FF0000" },
            new() { Name = "wave", Kind = LibraryKind.Effect, Color = "#000000", ColorLast = "#00FF00", Effect = LightEffectKind.SineWave, T1 = 400 },
            new() { Name = "square", Kind = LibraryKind.Effect, Color = "#000000", ColorLast = "#00FF00", Effect = LightEffectKind.SquareWave, T1 = 100, T2 = 100 },
        };
        var timeline = Timeline(timelineKind, timelineKind == TimelineKind.Pixel ? leds : 0);

        double start = 0;
        int placed = 0;
        foreach (var entry in entries)
        {
            if (Library.CanPlace(entry, timeline) != PlacementProblem.None) continue;
            var item = Library.NewItem(entry, timeline);
            item.Start = S(start);
            item.Duration = S(1);
            start += 1.5;
            _history.Do(Edits.AddItem(timeline, item));
            placed++;
        }
        Assert.True(placed > 0);

        var device = new DeviceBinding { DeviceType = deviceKind, LedCount = leds, TimelineIds = [timeline.Id] };
        var result = new ShowBuilder(_project, new ShowMedia(id => new MemoryStream(_files[id]))).Build(device);

        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Code)));
    }
}
