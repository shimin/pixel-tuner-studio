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
}
