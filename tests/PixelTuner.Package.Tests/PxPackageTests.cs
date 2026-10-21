using System.IO.Compression;
using System.Text;
using PixelTuner.Core.Model;

namespace PixelTuner.Package.Tests;

public sealed class PxPackageTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("pxpkg-tests").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    string Write(string name, byte[] content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    [Fact]
    public void Round_trip_keeps_project_and_media()
    {
        var audio = Write("track.mp3", Encoding.ASCII.GetBytes("ID3 fake mp3 data"));
        var image = Write("frame.bmp", new byte[2048]);

        var writer = new PxPackageWriter();
        var audioId = writer.AddMediaFile(audio);
        var imageId = writer.AddMediaFile(image);
        writer.AddEntry("cache/waveform/" + audioId + ".bin", [1, 2, 3]);

        var project = new ShowProject
        {
            Name = "Открытие",
            AudioResourceId = audioId,
            Timelines =
            [
                new Timeline
                {
                    Name = "Обруч 1",
                    Items = [new TimelineItem { Kind = "image", Start = TimeSpan.FromSeconds(1), Duration = TimeSpan.FromSeconds(2), ResourceId = imageId }],
                },
            ],
            Markers = [new Marker { Name = "Припев", Position = TimeSpan.FromSeconds(30) }],
        };

        var path = Path.Combine(_dir, "show.pxpkg");
        writer.Save(path, project);

        using var pkg = PxPackage.Open(path);
        Assert.Equal(project.Id, pkg.Manifest.ProjectId);
        Assert.Equal("Открытие", pkg.Project.Name);
        Assert.Equal(imageId, pkg.Project.Timelines[0].Items[0].ResourceId);
        Assert.Equal(TimeSpan.FromSeconds(30), pkg.Project.Markers[0].Position);
        Assert.Equal(File.ReadAllBytes(audio), pkg.ReadResource(audioId));
        Assert.Equal("track.mp3", pkg.GetResource(audioId).OriginalName);
        Assert.StartsWith("media/audio/", pkg.GetResource(audioId).Path);
        Assert.StartsWith("media/images/", pkg.GetResource(imageId).Path);
        Assert.Equal([1, 2, 3], pkg.TryReadEntry("cache/waveform/" + audioId + ".bin"));
        Assert.Empty(pkg.VerifyResources());
    }

    [Fact]
    public void Same_media_is_stored_once()
    {
        var a = Write("a.png", [1, 2, 3, 4]);
        var b = Write("b.png", [1, 2, 3, 4]);
        var writer = new PxPackageWriter();
        Assert.Equal(writer.AddMediaFile(a), writer.AddMediaFile(b));

        var path = Path.Combine(_dir, "dedup.pxpkg");
        writer.Save(path, new ShowProject());
        using var pkg = PxPackage.Open(path);
        Assert.Single(pkg.Manifest.Resources);
    }

    [Fact]
    public void Mimetype_is_the_first_entry_and_stored()
    {
        var path = Path.Combine(_dir, "mime.pxpkg");
        new PxPackageWriter().Save(path, new ShowProject());

        using var zip = ZipFile.OpenRead(path);
        var first = zip.Entries[0];
        Assert.Equal("mimetype", first.FullName);
        Assert.Equal(first.Length, first.CompressedLength);

        // The MIME type is readable at a fixed offset, like in ODF and EPUB.
        var head = File.ReadAllBytes(path)[30..(30 + 8 + PxPackageFormat.MimeType.Length)];
        Assert.Equal("mimetype" + PxPackageFormat.MimeType, Encoding.ASCII.GetString(head));
    }

    [Fact]
    public void Compressed_media_is_stored_without_recompression()
    {
        var mp3 = Write("t.mp3", Enumerable.Repeat((byte)7, 10_000).ToArray());
        var writer = new PxPackageWriter();
        var id = writer.AddMediaFile(mp3);
        var path = Path.Combine(_dir, "stored.pxpkg");
        writer.Save(path, new ShowProject());

        using var zip = ZipFile.OpenRead(path);
        var entry = zip.GetEntry($"media/audio/{id}.mp3")!;
        Assert.Equal(entry.Length, entry.CompressedLength);
    }

    [Fact]
    public void Saving_over_an_existing_file_replaces_it_and_leaves_no_temp_files()
    {
        var path = Path.Combine(_dir, "over.pxpkg");
        new PxPackageWriter().Save(path, new ShowProject { Name = "first" });
        new PxPackageWriter().Save(path, new ShowProject { Name = "second" });

        using (var pkg = PxPackage.Open(path))
            Assert.Equal("second", pkg.Project.Name);
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public void Rejects_a_zip_that_is_not_a_package()
    {
        var path = Path.Combine(_dir, "other.zip");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            zip.CreateEntry("readme.txt");
        Assert.Throws<InvalidDataException>(() => PxPackage.Open(path));
    }

    [Fact]
    public void Build_settings_timeline_height_and_picture_sizes_survive_a_round_trip()
    {
        var project = new ShowProject
        {
            Build = new BuildSettings { Depth = PixelTuner.Core.Export.ColorDepth.Bit36, GapStretching = 9, LightRate = 20 },
            Timelines = [new Timeline { Height = 125, Items = [new TimelineItem { MediaWidth = 40, MediaHeight = 125 }] }],
        };
        var path = Path.Combine(_dir, "settings.pxpkg");
        new PxPackageWriter().Save(path, project);

        using var pkg = PxPackage.Open(path);
        Assert.Equal((PixelTuner.Core.Export.ColorDepth.Bit36, 9, 20), (pkg.Project.Build.Depth, pkg.Project.Build.GapStretching, pkg.Project.Build.LightRate));
        Assert.Equal(125, pkg.Project.Timelines[0].Height);
        Assert.Equal((40, 125), (pkg.Project.Timelines[0].Items[0].MediaWidth, pkg.Project.Timelines[0].Items[0].MediaHeight));
    }

    [Fact]
    public void A_package_from_before_the_settings_reads_with_the_defaults_of_PixelTuner_2()
    {
        var path = Path.Combine(_dir, "old.pxpkg");
        new PxPackageWriter().Save(path, new ShowProject { Timelines = [new Timeline { Items = [new TimelineItem()] }] });

        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            var entry = zip.GetEntry("project.json")!;
            string json;
            using (var r = new StreamReader(entry.Open()))
                json = r.ReadToEnd();
            entry.Delete();
            // Cut out what older versions did not write.
            json = System.Text.RegularExpressions.Regex.Replace(json, "\"(build|height|mediaWidth|mediaHeight)\": (\\{[^}]*\\}|\\d+),?", "");
            Assert.DoesNotContain("\"build\"", json);
            using var w = new StreamWriter(zip.CreateEntry("project.json").Open());
            w.Write(json);
        }

        using var pkg = PxPackage.Open(path);
        Assert.Equal((PixelTuner.Core.Export.ColorDepth.Bit24, 4, 5), (pkg.Project.Build.Depth, pkg.Project.Build.GapStretching, pkg.Project.Build.LightRate));
        Assert.Equal(0, pkg.Project.Timelines[0].Height);
        Assert.Equal(0, pkg.Project.Timelines[0].Items[0].MediaHeight);
    }

    [Fact]
    public void Rejects_a_package_from_a_newer_version()
    {
        var path = Path.Combine(_dir, "new.pxpkg");
        new PxPackageWriter().Save(path, new ShowProject());

        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            var manifest = zip.GetEntry("manifest.json")!;
            string json;
            using (var r = new StreamReader(manifest.Open()))
                json = r.ReadToEnd();
            manifest.Delete();
            using var w = new StreamWriter(zip.CreateEntry("manifest.json").Open());
            w.Write(json.Replace("\"minReaderVersion\": 1", "\"minReaderVersion\": 99"));
        }

        var ex = Assert.Throws<PackageTooNewException>(() => PxPackage.Open(path));
        Assert.Equal(99, ex.MinReaderVersion);
    }

    [Fact]
    public void Detects_damaged_media()
    {
        var png = Write("x.png", [9, 9, 9]);
        var writer = new PxPackageWriter();
        var id = writer.AddMediaFile(png);
        var path = Path.Combine(_dir, "broken.pxpkg");
        writer.Save(path, new ShowProject());

        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            var e = zip.GetEntry($"media/images/{id}.png")!;
            e.Delete();
            using var s = zip.CreateEntry($"media/images/{id}.png").Open();
            s.Write([0, 0, 0]);
        }

        using var pkg = PxPackage.Open(path);
        Assert.Equal([id], pkg.VerifyResources());
    }
}
