using PixelTuner.Package.Import;
using PixelTuner.Core.Editing;
using PixelTuner.Core.Model;

namespace PixelTuner.Package.Tests;

public class ProjectDocumentTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pxdoc").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string PackageWithMedia(string name = "show.pxpkg")
    {
        var writer = new PxPackageWriter();
        string id = writer.AddMedia([1, 2, 3, 4, 5], "tone.wav");
        string path = Path.Combine(_dir, name);
        writer.Save(path, new ShowProject { Name = "Show", AudioResourceId = id });
        return path;
    }

    [Fact]
    public void A_new_project_is_clean_until_it_is_edited()
    {
        using var doc = ProjectDocument.CreateNew("Draft");
        Assert.False(doc.IsDirty);
        Assert.Null(doc.Path);

        int changes = 0;
        doc.Changed += (_, _) => changes++;
        doc.Name = "Draft 2";
        doc.Name = "Draft 2";   // the same value is no change

        Assert.True(doc.IsDirty);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Save_without_a_file_is_refused()
    {
        using var doc = ProjectDocument.CreateNew();
        Assert.Throws<InvalidOperationException>(doc.Save);
    }

    [Fact]
    public void SaveAs_writes_the_package_and_clears_the_flag()
    {
        using var doc = ProjectDocument.CreateNew("A");
        doc.Name = "B";
        string path = Path.Combine(_dir, "b.pxpkg");

        doc.SaveAs(path);

        Assert.False(doc.IsDirty);
        Assert.Equal(path, doc.Path);
        using var again = PxPackage.Open(path);
        Assert.Equal("B", again.Project.Name);
    }

    [Fact]
    public void Save_over_the_opened_file_keeps_the_media()
    {
        string path = PackageWithMedia();
        using var doc = ProjectDocument.Open(path);
        var created = PxPackage.Open(path).Manifest.CreatedUtc;
        doc.Name = "Renamed";

        doc.Save();   // the file is open for media while it is replaced

        Assert.False(doc.IsDirty);
        using var again = PxPackage.Open(path);
        Assert.Equal("Renamed", again.Project.Name);
        Assert.Equal(created, again.Manifest.CreatedUtc);
        string id = again.Project.AudioResourceId!;
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, again.ReadResource(id));
        Assert.Empty(again.VerifyResources());
        // The document still reads media after the save.
        Assert.Equal(1, doc.MediaCount);
        Assert.NotNull(doc.CreateMedia());
    }

    [Fact]
    public void SaveAs_to_another_file_copies_the_media_and_leaves_the_original()
    {
        string path = PackageWithMedia();
        using var doc = ProjectDocument.Open(path);
        string copy = Path.Combine(_dir, "copy.pxpkg");

        doc.SaveAs(copy);

        Assert.Equal(copy, doc.Path);
        using var a = PxPackage.Open(path);
        using var b = PxPackage.Open(copy);
        Assert.Equal(a.Project.AudioResourceId, b.Project.AudioResourceId);
        Assert.Equal(a.ReadResource(a.Project.AudioResourceId!), b.ReadResource(b.Project.AudioResourceId!));
        Assert.Empty(b.VerifyResources());
    }

    [Fact]
    public void A_project_opened_from_a_stream_has_no_path_and_saves_with_SaveAs()
    {
        string path = PackageWithMedia();
        using var doc = ProjectDocument.Open(new MemoryStream(File.ReadAllBytes(path)));
        Assert.Null(doc.Path);
        Assert.Throws<InvalidOperationException>(doc.Save);

        string copy = Path.Combine(_dir, "from-stream.pxpkg");
        doc.SaveAs(copy);

        using var again = PxPackage.Open(copy);
        Assert.Empty(again.VerifyResources());
        Assert.Single(again.Manifest.Resources);
    }

    [Fact]
    public void A_failed_save_keeps_the_document_usable_and_dirty()
    {
        string path = PackageWithMedia();
        using var doc = ProjectDocument.Open(path);
        doc.Name = "Changed";

        // A folder cannot be made under a file, so this save fails.
        Assert.ThrowsAny<Exception>(() => doc.SaveAs(Path.Combine(path, "inside", "x.pxpkg")));

        Assert.True(doc.IsDirty);
        Assert.Equal(path, doc.Path);
        Assert.NotNull(doc.CreateMedia());
        doc.Save();
        Assert.False(doc.IsDirty);
    }

    [Fact]
    public void Undo_brings_back_the_saved_state_and_clears_the_flag()
    {
        string path = PackageWithMedia();
        using var doc = ProjectDocument.Open(path);

        doc.Name = "Edited";
        Assert.True(doc.IsDirty);
        Assert.True(doc.CanUndo);
        Assert.Equal("RenameProject", doc.UndoKey);

        doc.Undo();

        Assert.Equal("Show", doc.Name);
        Assert.False(doc.IsDirty);
        Assert.True(doc.CanRedo);
        doc.Redo();
        Assert.Equal("Edited", doc.Name);
        Assert.True(doc.IsDirty);
    }

    [Fact]
    public void Saving_keeps_the_history_and_undo_after_it_is_a_change_again()
    {
        string path = PackageWithMedia();
        using var doc = ProjectDocument.Open(path);
        doc.Execute(Edits.AddMarker(doc.Project, new Marker { Name = "drop", Position = TimeSpan.FromSeconds(3) }));

        doc.Save();
        Assert.False(doc.IsDirty);
        Assert.True(doc.CanUndo);            // the history survives the save

        doc.Undo();
        Assert.True(doc.IsDirty);            // the file still has the marker
        doc.Save();

        using var again = PxPackage.Open(path);
        Assert.Empty(again.Project.Markers);
    }

    [Fact]
    public void Changed_is_raised_for_edits_undo_redo_and_save()
    {
        string path = PackageWithMedia();
        using var doc = ProjectDocument.Open(path);
        int raised = 0;
        doc.Changed += (_, _) => raised++;

        doc.Name = "X";
        doc.Undo();
        doc.Redo();
        doc.Save();

        Assert.Equal(4, raised);
    }

    [Fact]
    public void A_fresh_document_has_nothing_to_undo()
    {
        using var doc = ProjectDocument.Open(PackageWithMedia());
        Assert.False(doc.CanUndo);
        Assert.False(doc.CanRedo);
        Assert.Null(doc.UndoKey);
    }

    // ---- soundtrack

    private string SoundFile(byte[] content, string name = "song.mp3")
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    [Fact]
    public void Setting_the_audio_adds_the_media_and_saving_writes_it_into_the_package()
    {
        using var doc = ProjectDocument.CreateNew("Show");
        string sound = SoundFile([9, 8, 7, 6]);

        doc.SetAudio(sound);
        File.Delete(sound);                                  // the project no longer needs the original

        Assert.NotNull(doc.Project.AudioResourceId);
        Assert.Equal(sound, doc.Project.AudioSourcePath);
        Assert.Equal("song.mp3", doc.MediaName(doc.Project.AudioResourceId));
        Assert.Equal(1, doc.MediaCount);
        Assert.True(doc.IsDirty);
        Assert.Equal("SetAudio", doc.UndoKey);
        using (var stream = doc.OpenMedia(doc.Project.AudioResourceId))
            Assert.Equal(new byte[] { 9, 8, 7, 6 }, ReadAll(stream!));

        string path = Path.Combine(_dir, "with-sound.pxpkg");
        doc.SaveAs(path);

        using var again = PxPackage.Open(path);
        Assert.Equal(doc.Project.AudioResourceId, again.Project.AudioResourceId);
        Assert.Equal(new byte[] { 9, 8, 7, 6 }, again.ReadResource(again.Project.AudioResourceId!));
        Assert.Empty(again.VerifyResources());
        Assert.Equal(1, doc.MediaCount);                     // now it is the package's, still counted once
    }

    [Fact]
    public void Undoing_the_audio_leaves_it_out_of_the_package_and_redo_still_has_it()
    {
        string path = PackageWithMedia();
        using var doc = ProjectDocument.Open(path);
        string sound = SoundFile([1, 1, 2, 3, 5]);
        doc.SetAudio(sound);
        string id = doc.Project.AudioResourceId!;
        Assert.NotEqual(PxPackage.Open(path).Project.AudioResourceId, id);   // not the package's own audio

        doc.Undo();
        doc.Save();
        using (var saved = PxPackage.Open(path))
            Assert.False(saved.HasResource(id));            // not referenced, not written

        doc.Redo();                                          // the media must still be there
        Assert.Equal(id, doc.Project.AudioResourceId);
        Assert.NotNull(doc.OpenMedia(id));
        doc.Save();
        using var again = PxPackage.Open(path);
        Assert.Equal(new byte[] { 1, 1, 2, 3, 5 }, again.ReadResource(id));
    }

    [Fact]
    public void The_same_sound_added_twice_is_one_media_file_and_the_package_media_is_readable()
    {
        string path = PackageWithMedia();
        using var doc = ProjectDocument.Open(path);
        string existing = doc.Project.AudioResourceId!;
        int before = doc.MediaCount;

        doc.SetAudio(SoundFile([1, 2, 3, 4, 5], "same.wav"));   // the same bytes the package already has

        Assert.Equal(existing, doc.Project.AudioResourceId);
        Assert.Equal(before, doc.MediaCount);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, ReadAll(doc.OpenMedia(existing)!));
        Assert.Equal("tone.wav", doc.MediaName(existing));
        Assert.Null(doc.OpenMedia("0000"));
        Assert.Null(doc.OpenMedia(null));
    }

    [Fact]
    public void Removing_the_audio_is_undoable()
    {
        string path = PackageWithMedia();
        using var doc = ProjectDocument.Open(path);
        string id = doc.Project.AudioResourceId!;

        doc.RemoveAudio();
        Assert.Null(doc.Project.AudioResourceId);

        doc.Undo();
        Assert.Equal(id, doc.Project.AudioResourceId);
        Assert.False(doc.IsDirty);
    }

    private static byte[] ReadAll(Stream stream)
    {
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    [Fact]
    public void Opening_an_imported_project_reads_the_picture_sizes_the_import_did_not_store()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "LegacyProjects", "80_sample");
        string package = Path.Combine(Directory.CreateTempSubdirectory("doc-sizes").FullName, "p.pxpkg");
        PtpImporter.Import(Path.Combine(dir, "80_sample.ptp"), package);

        using var document = ProjectDocument.Open(package);

        var pictures = document.Project.Timelines.Where(t => t.Kind == TimelineKind.Pixel).SelectMany(t => t.Items).ToList();
        Assert.NotEmpty(pictures);
        Assert.All(pictures, p => Assert.True(p.MediaWidth > 0 && p.MediaHeight > 0));
        // The height of a timeline is what its pictures are: 80 LEDs in this show.
        Assert.All(document.Project.Timelines.Where(t => t.Kind == TimelineKind.Pixel), t => Assert.Equal(80, Edits.PixelHeight(t)));
        Assert.False(document.IsDirty);
    }
}
