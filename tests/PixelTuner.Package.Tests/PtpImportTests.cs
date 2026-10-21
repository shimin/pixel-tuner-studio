using System.Text;
using PixelTuner.Core.Export;
using PixelTuner.Core.Legacy;
using PixelTuner.Core.Model;
using PixelTuner.Package.Import;

namespace PixelTuner.Package.Tests;

/// <summary>
/// A PixelTuner 2 project imported into a .pxpkg must compile to the same .bin as the
/// project itself. The direct export is checked against PixelTuner 2's own files in
/// PixelTuner.Core.Tests, so equality here means the import loses nothing the devices need.
/// </summary>
public sealed class PtpImportTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ptp-import-tests").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    internal static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "LegacyProjects", name);

    public static TheoryData<string, string> Projects => new()
    {
        { "80_sample", "80_sample.ptp" },
        { "bugi", "60_bugi.ptp" },
        { "36_fan", "36_fans.ptp" },
        { "125_newYear", "125_newYear.ptp" },
        { "150_stavropol", "150_stavrop.ptp" },
        { "cheer_power", "cheer_power.ptp" },
        { "glasses", "glasses.ptp" },
    };

    [Theory]
    [MemberData(nameof(Projects))]
    public void Imported_package_exports_the_same_bin_as_the_project(string folder, string file)
    {
        string ptp = Path.Combine(Fixture(folder), file);
        string package = Path.Combine(_dir, folder + PxPackageFormat.Extension);
        var result = PtpImporter.Import(ptp, package);
        Assert.Empty(result.MissingMedia);

        using var pkg = PxPackage.Open(package);
        Assert.Empty(pkg.VerifyResources());
        int compared = ExportComparison.Compare(PtpDocument.Load(ptp), Fixture(folder), pkg);
        Assert.True(compared > 0, "No timeline was compared.");
    }

    [Fact]
    public void Import_keeps_timelines_markup_canvas_and_shares_media()
    {
        string ptp = Path.Combine(Fixture("glasses"), "glasses.ptp");
        var document = PtpDocument.Load(ptp);
        string package = Path.Combine(_dir, "glasses.pxpkg");
        PtpImporter.Import(ptp, package);

        using var pkg = PxPackage.Open(package);
        var project = pkg.Project;
        Assert.Equal("glasses", project.Name);
        Assert.Equal(document.Timelines.Count, project.Timelines.Count);
        Assert.All(project.Timelines, t => Assert.Equal(TimelineKind.Video, t.Kind));
        Assert.Equal(document.CanvasElements.Count, project.LedLayouts.Count);

        var line = document.Timelines[0];
        var timeline = project.Timelines[0];
        Assert.Equal(line.Attributes["LineTag"], timeline.Name);
        var layout = Assert.Single(project.LedLayouts, l => l.Id == timeline.LedLayoutId);
        Assert.Equal(line.Attributes["MetaDataPath"], layout.Name);
        Assert.Equal(document.CanvasElements[layout.Name].OrderedLeds().Count, layout.Leds.Count);
        Assert.Equal(line.Items.Count, timeline.Items.Count);
        Assert.Equal(line.Items[0].End, timeline.Items[0].End);
        Assert.Equal(line.Items[0].FullName, timeline.Items[0].SourcePath);

        Assert.NotNull(project.CanvasResourceId);
        Assert.Equal(document.CanvasContent, Encoding.UTF8.GetString(pkg.ReadResource(project.CanvasResourceId)));
        Assert.Equal("canvas.svg", pkg.GetResource(project.CanvasResourceId).OriginalName);

        // Each distinct file is stored once however many items use it.
        int distinctFiles = project.Timelines.SelectMany(t => t.Items).Select(i => i.ResourceId).Distinct().Count();
        Assert.Equal(distinctFiles + 1, pkg.Manifest.Resources.Count);
    }

    [Fact]
    public void Missing_media_is_asked_for_once_and_can_be_replaced()
    {
        string project = Path.Combine(_dir, "show");
        Directory.CreateDirectory(project);
        string image = Path.Combine(project, "a.bmp");
        File.Copy(Path.Combine(Fixture("80_sample"), Directory.GetFiles(Fixture("80_sample"), "*.bmp").Select(Path.GetFileName).First()!), image);
        string replacement = Path.Combine(_dir, "found.bmp");
        File.Copy(image, replacement);
        File.WriteAllBytes(Path.Combine(project, "track.mp3"), Encoding.ASCII.GetBytes("ID3 not really mp3"));
        string ptp = Path.Combine(project, "show.ptp");
        File.WriteAllText(ptp, $"""
            <?xml version="1.0"?>
            <FileViewModel xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" AudioPath="D:\Shows\track.mp3">
              <Line xsi:type="PixelTimeLineViewModel" LineTag="Poi" LineColorTagXml="#FF102030">
                <Item Start="00:00:00.000" End="00:00:01.000" FullName="./a.bmp" Stretching="2" Brightness="80" LaserOn="true" />
                <Item Start="00:00:01.000" End="00:00:02.000" FullName="C:\Users\me\gone.bmp" />
                <Item Start="00:00:02.500" End="00:00:03.000" FullName="C:\Users\me\gone.bmp" />
                <Item Start="00:00:03.000" End="00:00:04.000" FullName="./lost.bmp" />
              </Line>
            </FileViewModel>
            """);

        var asked = new List<string>();
        var result = PtpImporter.Import(ptp, Path.Combine(_dir, "show.pxpkg"), new PtpImportOptions
        {
            FindMissing = path =>
            {
                asked.Add(path);
                return path.EndsWith("gone.bmp", StringComparison.Ordinal) ? replacement : null;
            },
        });

        Assert.Equal([@"C:\Users\me\gone.bmp", "./lost.bmp"], asked);
        Assert.Equal(["./lost.bmp"], result.MissingMedia);

        using var pkg = PxPackage.Open(Path.Combine(_dir, "show.pxpkg"));
        var items = pkg.Project.Timelines[0].Items;
        Assert.Equal(items[0].ResourceId, items[1].ResourceId);   // same content as the replacement
        Assert.Equal(items[1].ResourceId, items[2].ResourceId);
        Assert.Null(items[3].ResourceId);
        Assert.Equal("./lost.bmp", items[3].SourcePath);
        Assert.Equal((2, (byte)80, true), (items[0].Stretching, items[0].Brightness, items[0].LaserOn));
        Assert.Equal("#FF102030", pkg.Project.Timelines[0].Color);
        Assert.Equal("Poi", pkg.Project.Timelines[0].Name);

        // The soundtrack was found next to the project although the stored path is absolute.
        Assert.Equal(@"D:\Shows\track.mp3", pkg.Project.AudioSourcePath);
        Assert.Equal("ID3 not really mp3", Encoding.ASCII.GetString(pkg.ReadResource(pkg.Project.AudioResourceId!)));
        Assert.StartsWith("media/audio/", pkg.GetResource(pkg.Project.AudioResourceId!).Path);

        // A pixel export needs every image, so the missing one is reported by name.
        var error = Assert.Throws<FileNotFoundException>(() =>
            new PixelExport(pkg.CreateMedia()).Export(pkg.Project.Timelines, 80));
        Assert.Contains("./lost.bmp", error.Message);
    }

    [Fact]
    public void Light_and_epic_timelines_survive_the_import()
    {
        string project = Path.Combine(_dir, "light");
        Directory.CreateDirectory(project);
        foreach (string bmp in Directory.GetFiles(Fixture("80_sample"), "*.bmp").Take(2))
            File.Copy(bmp, Path.Combine(project, Path.GetFileName(bmp)));
        var images = Directory.GetFiles(project).Select(Path.GetFileName).ToArray();
        string ptp = Path.Combine(project, "light.ptp");
        File.WriteAllText(ptp, $"""
            <?xml version="1.0"?>
            <FileViewModel xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <Line xsi:type="LightTimeLineViewModel" IsMonochrome="true" LineTag="Wash">
                <Item xsi:type="TimeLineItemSolid" Start="00:00:00.100" End="00:00:00.400" FullName="pack://application:,,,/Effects/04_solid.png" Color="#FF20A040" />
                <Item xsi:type="TimeLineItemEffect" Start="00:00:00.400" End="00:00:01.200" EffectID="Sine Wave"><Data ColorFirst="#FF000000" ColorLast="#FFFF8000" T1="120" T2="40" /></Item>
                <Item xsi:type="TimeLineItemEffect" Start="00:00:01.500" End="00:00:02.000" EffectID="Saw-tooth Wave"><Data ColorFirst="#F00" ColorLast="#FF0000FF" T1="90" T2="0" /></Item>
              </Line>
              <Line xsi:type="LightTimeLineViewModel">
                <Item xsi:type="TimeLineItemEffect" Start="00:00:00.000" End="00:00:00.900" EffectID="Linear Transition"><Data ColorFirst="#FF112233" ColorLast="#FF445566" T1="0" T2="0" /></Item>
                <Item xsi:type="TimeLineItemEffect" Start="00:00:00.900" End="00:00:01.300" EffectID="Square Wave"><Data ColorFirst="#FF010203" ColorLast="#FFFFFFFF" T1="50" T2="25" /></Item>
              </Line>
              <Line xsi:type="EpicPoiTimeLineViewModel" LineColorTagXml="#FF405060">
                <Item xsi:type="TimeLineItemGradient" Start="00:00:00.200" End="00:00:01.000" FullName="./{images[0]}" Stretching="3" Brightness="70" LaserOn="true" />
                <Item xsi:type="TimeLineItemGradient" Start="00:00:01.000" End="00:00:01.500" FullName="./missing.png" Stretching="1" Brightness="100" />
                <Item xsi:type="TimeLineItemGradient" Start="00:00:02.000" End="00:00:03.000" FullName="./{images[1]}" Stretching="2" Brightness="50" />
              </Line>
            </FileViewModel>
            """);

        string package = Path.Combine(_dir, "light.pxpkg");
        var result = PtpImporter.Import(ptp, package);
        Assert.Equal(["./missing.png"], result.MissingMedia);

        using var pkg = PxPackage.Open(package);
        Assert.Equal(3, ExportComparison.Compare(PtpDocument.Load(ptp), project, pkg));
        Assert.True(pkg.Project.Timelines[0].Monochrome);
        Assert.Equal(LightEffectKind.SineWave, pkg.Project.Timelines[0].Items[1].Effect);
        Assert.Null(pkg.Project.Timelines[0].Items[0].ResourceId);   // the app's own thumbnail is no media
    }
}

/// <summary>Exports every timeline of a project both from the .ptp and from its package.</summary>
internal static class ExportComparison
{
    /// <returns>How many exports produced a .bin (and matched); failures count only when both sides fail the same way.</returns>
    public static int Compare(PtpDocument document, string projectDirectory, PxPackage package)
    {
        var resolver = new PtpMediaResolver(projectDirectory);
        string? Resolve(string path)
        {
            try { return resolver.Resolve(path); }
            catch (FileNotFoundException) { return null; }
        }
        var project = package.Project;
        Assert.Equal(document.Timelines.Count, project.Timelines.Count);
        int compared = 0;

        var pixelDirect = new PtpPixelExport(resolver.Resolve);
        var pixelPackage = new PixelExport(package.CreateMedia());
        var ptpPixel = PtpPixelExport.PixelTimelines(document);
        var pixel = project.Timelines.Where(t => t.Kind == TimelineKind.Pixel).ToList();
        var heights = new HashSet<int>();
        for (int i = 0; i < pixel.Count; i++)
        {
            if (pixel[i].Items.Count == 0) continue;
            var (direct, imported) = (ptpPixel[i], pixel[i]);
            if (SameResult(
                () => pixelDirect.Export([direct], checked((ushort)pixelDirect.TimelineHeight(direct))),
                () => pixelPackage.Export([imported], checked((ushort)pixelPackage.TimelineHeight(imported)))))
            {
                heights.Add(pixelPackage.TimelineHeight(imported));
                SameResult(() => pixelDirect.ExportFan(direct), () => pixelPackage.ExportFan(imported));
                compared++;
            }
        }
        foreach (int height in heights)
            SameResult(() => pixelDirect.Export(ptpPixel, (ushort)height), () => pixelPackage.Export(pixel, (ushort)height));

        var videoDirect = new PtpVideoExport(document, Resolve);
        var videoPackage = new VideoExport(project, package.CreateMedia());
        var ptpVideo = PtpVideoExport.VideoTimelines(document);
        var video = project.Timelines.Where(t => t.Kind == TimelineKind.Video).ToList();
        for (int i = 0; i < video.Count; i++)
        {
            var (direct, imported) = (ptpVideo[i], video[i]);
            Assert.Equal(videoDirect.Leds(direct), videoPackage.Leds(imported));
            if (SameResult(() => videoDirect.Export([direct]), () => videoPackage.Export([imported]))) compared++;
        }
        if (video.Count > 1)
            SameResult(() => videoDirect.Export(ptpVideo), () => videoPackage.Export(video));

        var epic = project.Timelines.Where(t => t.Kind == TimelineKind.EpicPoi).ToList();
        if (epic.Count > 0)
        {
            bool ok = true;
            foreach (var depth in Enum.GetValues<ColorDepth>())
                ok &= SameResult(() => new PtpEpicExport(Resolve) { Depth = depth }.Export(PtpEpicExport.EpicTimelines(document)),
                    () => new EpicExport(package.CreateMedia()) { Depth = depth }.Export(epic));
            if (ok) compared++;
        }

        var light = project.Timelines.Where(t => t.Kind == TimelineKind.Light).ToList();
        if (light.Count > 0)
        {
            bool ok = true;
            foreach (int rate in new[] { 1, 5, 20 })
                ok &= SameResult(() => PtpLightExport.Export(PtpLightExport.LightTimelines(document), rate), () => LightExport.Export(light, rate));
            if (ok) compared += light.Count;
        }
        return compared;
    }

    /// <summary>
    /// Same bytes, or the same error where neither can export (a missing file, or two video
    /// timelines PixelTuner 2 could not pair). Returns true when a .bin was produced.
    /// </summary>
    private static bool SameResult(Func<byte[]> direct, Func<byte[]> fromPackage)
    {
        byte[] expected;
        try { expected = direct(); }
        catch (Exception error)
        {
            var actual = Assert.Throws(error.GetType(), () => fromPackage());
            Assert.Equal(error.Message, actual.Message);
            return false;
        }
        Assert.Equal(expected, fromPackage());
        return true;
    }
}
