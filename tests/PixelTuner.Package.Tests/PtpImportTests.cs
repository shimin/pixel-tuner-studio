using System.Text;
using PixelTuner.Core.Export;
using PixelTuner.Core.Legacy;
using PixelTuner.Core.Model;
using PixelTuner.Package.Import;

namespace PixelTuner.Package.Tests;

/// <summary>
/// .ptp → .pxpkg: the package keeps everything the devices need, so the .bin made from the
/// opened package is the .bin made right after the import (which the Core tests compare with
/// PixelTuner 2).
/// </summary>
public sealed class PtpImportTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("ptp-import-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static string Fixture(string folder, string project) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "LegacyProjects", folder, project);

    private (PtpImportResult Imported, PxPackage Package) ImportAndReopen(string ptp)
    {
        var imported = PtpPackageImport.Import(ptp);
        string path = Path.Combine(_dir, Path.GetFileNameWithoutExtension(ptp) + PxPackageFormat.Extension);
        PtpPackageImport.Save(imported, path);
        return (imported, PxPackage.Open(path));
    }

    [Theory]
    [InlineData("80_sample", "80_sample.ptp")]
    [InlineData("bugi", "60_bugi.ptp")]
    [InlineData("36_fan", "36_fans.ptp")]
    [InlineData("125_newYear", "125_newYear.ptp")]
    [InlineData("150_stavropol", "150_stavrop.ptp")]
    [InlineData("cheer_power", "cheer_power.ptp")]
    public void Pixel_show_exports_the_same_from_the_package(string folder, string project)
    {
        var (imported, package) = ImportAndReopen(Fixture(folder, project));
        using var _ = package;
        Assert.Empty(imported.Missing);

        var before = new PixelExport(imported.Media);
        var after = new PixelExport(package);
        var linesBefore = PixelExport.PixelTimelines(imported.Project);
        var linesAfter = PixelExport.PixelTimelines(package.Project);
        Assert.Equal(linesBefore.Count, linesAfter.Count);
        foreach (int height in linesBefore.Select(before.TimelineHeight).Distinct())
            Assert.Equal(before.Export(linesBefore, (ushort)height), after.Export(linesAfter, (ushort)height));
        for (int i = 0; i < linesBefore.Count; i++)
            Assert.Equal(before.ExportFan(linesBefore[i]), after.ExportFan(linesAfter[i]));
    }

    [Fact]
    public void Video_show_exports_the_same_from_the_package()
    {
        var (imported, package) = ImportAndReopen(Fixture("glasses", "glasses.ptp"));
        using var _ = package;
        Assert.Empty(imported.Missing);

        var linesBefore = VideoExport.VideoTimelines(imported.Project);
        var linesAfter = VideoExport.VideoTimelines(package.Project);
        Assert.Equal(2, linesAfter.Count);
        var before = new VideoExport(imported.Project, imported.Media);
        var after = new VideoExport(package.Project, package);
        Assert.NotEmpty(after.Leds(linesAfter[0]));
        Assert.Equal(before.Export([linesBefore[0]]), after.Export([linesAfter[0]]));
        Assert.Equal(before.Export([linesBefore[1]]), after.Export([linesAfter[1]]));

        // The canvas drawing travels with the show.
        Assert.NotNull(package.Project.Canvas?.DrawingResourceId);
        Assert.StartsWith("<?xml", Encoding.UTF8.GetString(package.ReadResource(package.Project.Canvas!.DrawingResourceId!)).TrimStart('\uFEFF'));
    }

    [Fact]
    public void Light_and_epic_show_exports_the_same_from_the_package()
    {
        File.WriteAllBytes(Path.Combine(_dir, "grad.bmp"), Bmp(3, [(255, 0, 0), (0, 255, 0), (0, 0, 255)]));
        string ptp = Path.Combine(_dir, "mixed.ptp");
        File.WriteAllText(ptp, """
            <?xml version="1.0"?>
            <FileViewModel xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" UnitSize="12.5">
              <Line xsi:type="EpicPoiTimeLineViewModel" LineTag="poi" LineColorTagXml="#FF102030">
                <Item xsi:type="TimeLineItemGradient" Start="00:00:00.500" End="00:00:02.000" FullName="./grad.bmp" Stretching="3" Brightness="80" LaserOn="true" />
                <Item xsi:type="TimeLineItemGradient" Start="00:00:02.000" End="00:00:03.000" FullName="C:\nowhere\lost.bmp" Stretching="1" Brightness="100" />
              </Line>
              <Line xsi:type="LightTimeLineViewModel" LineTag="spot" IsMonochrome="true">
                <Item xsi:type="TimeLineItemSolid" Start="00:00:00.000" End="00:00:01.000" FullName="pack://application:,,,/Effects/04_solid.png" Color="#FF804020" />
                <Item xsi:type="TimeLineItemEffect" Start="00:00:01.500" End="00:00:04.000" EffectID="Sine Wave"><Data ColorFirst="#FF000000" ColorLast="#FFFF8000" T1="700" T2="0" /></Item>
              </Line>
            </FileViewModel>
            """);
        var (imported, package) = ImportAndReopen(ptp);
        using var _ = package;

        Assert.Equal(["C:\\nowhere\\lost.bmp"], imported.Missing);
        Assert.Empty(imported.Warnings);
        var lost = package.Project.Timelines[0].Items[1];
        Assert.Null(lost.ResourceId);
        Assert.Equal("C:\\nowhere\\lost.bmp", lost.SourceName);
        Assert.Equal("12.5", package.Project.Extra!["UnitSize"]);

        var light = package.Project.Timelines[1];
        Assert.True(light.Monochrome);
        Assert.Equal("#804020", light.Items[0].Color);
        Assert.Equal(EffectKind.SineWave, light.Items[1].Effect);
        Assert.Equal("#FF8000", light.Items[1].ColorLast);
        Assert.Equal(700, light.Items[1].T1);

        Assert.Equal(
            new EpicExport(imported.Media).Export(EpicExport.EpicTimelines(imported.Project)),
            new EpicExport(package).Export(EpicExport.EpicTimelines(package.Project)));
        Assert.Equal(
            LightExport.Export(LightExport.LightTimelines(imported.Project)),
            LightExport.Export(LightExport.LightTimelines(package.Project)));
    }

    [Fact]
    public void Missing_files_are_found_again_with_the_users_answer()
    {
        string show = Path.Combine(_dir, "show");
        string elsewhere = Path.Combine(_dir, "elsewhere");
        Directory.CreateDirectory(show);
        Directory.CreateDirectory(elsewhere);
        File.WriteAllBytes(Path.Combine(elsewhere, "Pattern.bmp"), Bmp(1, [(1, 2, 3)]));
        File.WriteAllBytes(Path.Combine(elsewhere, "picked.bmp"), Bmp(1, [(4, 5, 6)]));
        string ptp = Path.Combine(show, "show.ptp");
        File.WriteAllText(ptp, """
            <?xml version="1.0"?>
            <FileViewModel xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" AudioPath="D:\music\song.mp3">
              <Line xsi:type="PixelTimeLineViewModel" LineColorTagXml="#FF0000FF">
                <Item Start="00:00:00.000" End="00:00:01.000" FullName="C:\shows\pattern.bmp" />
                <Item Start="00:00:01.000" End="00:00:02.000" FullName="C:\shows\other.bmp" />
              </Line>
            </FileViewModel>
            """);

        var first = PtpPackageImport.Import(ptp);
        Assert.Equal([@"D:\music\song.mp3", @"C:\shows\pattern.bmp", @"C:\shows\other.bmp"], first.Missing);
        Assert.Equal(@"D:\music\song.mp3", first.Project.AudioSourceName);
        Assert.Null(first.Project.AudioResourceId);

        var second = PtpPackageImport.Import(ptp,
            replacements: new Dictionary<string, string> { [@"C:\shows\other.bmp"] = Path.Combine(elsewhere, "picked.bmp") },
            searchFolders: [elsewhere]);
        Assert.Equal([@"D:\music\song.mp3"], second.Missing);
        Assert.All(second.Project.Timelines[0].Items, i => Assert.NotNull(i.ResourceId));
        Assert.NotEqual(second.Project.Timelines[0].Items[0].ResourceId, second.Project.Timelines[0].Items[1].ResourceId);
    }

    /// <summary>A one-row 24-bit BMP.</summary>
    private static byte[] Bmp(int width, (byte R, byte G, byte B)[] colors)
    {
        int stride = (width * 3 + 3) & ~3;
        var bmp = new byte[54 + stride];
        bmp[0] = (byte)'B';
        bmp[1] = (byte)'M';
        BitConverter.GetBytes(bmp.Length).CopyTo(bmp, 2);
        BitConverter.GetBytes(54).CopyTo(bmp, 10);
        BitConverter.GetBytes(40).CopyTo(bmp, 14);
        BitConverter.GetBytes(width).CopyTo(bmp, 18);
        BitConverter.GetBytes(1).CopyTo(bmp, 22);
        BitConverter.GetBytes((short)1).CopyTo(bmp, 26);
        BitConverter.GetBytes((short)24).CopyTo(bmp, 28);
        for (int x = 0; x < width; x++)
        {
            bmp[54 + 3 * x] = colors[x].B;
            bmp[55 + 3 * x] = colors[x].G;
            bmp[56 + 3 * x] = colors[x].R;
        }
        return bmp;
    }
}
