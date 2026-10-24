using System.Buffers.Binary;
using PixelTuner.Core.Export;
using PixelTuner.Core.Model;
using Xunit.Abstractions;

namespace PixelTuner.Core.Tests;

/// <summary>
/// Video-device .bin files of the reference set (see <see cref="FullSampleExportTests"/>).
/// "N.bin" is video timeline N alone; any other name is all video timelines, of which
/// PixelTuner 2 exported the first two as a pair.
/// </summary>
public class FullSampleVideoExportTests(ITestOutputHelper output)
{
    [Fact]
    public void Every_video_bin_in_the_reference_set_matches()
    {
        string? root = Environment.GetEnvironmentVariable("PIXELTUNER_SAMPLES");
        if (string.IsNullOrEmpty(root))
        {
            output.WriteLine("PIXELTUNER_SAMPLES is not set, nothing to check.");
            return;
        }

        int checkedFiles = 0;
        var failures = new List<string>();
        foreach (string dir in Directory.EnumerateDirectories(root).Order())
        {
            string binDir = Path.Combine(dir, "bin");
            if (!Directory.Exists(binDir)) continue;
            var projects = Directory.EnumerateFiles(dir, "*.ptp").Order()
                .Select(p => (Path: p, Show: Shows.Import(p)))
                .Where(p => VideoExport.VideoTimelines(p.Show.Project).Count > 0)
                .ToList();
            if (projects.Count == 0) continue;

            foreach (string binPath in Directory.EnumerateFiles(binDir, "*.bin").Order())
            {
                byte[] expected = File.ReadAllBytes(binPath);
                string name = Path.GetFileNameWithoutExtension(binPath);
                if (expected.Length >= 12 && BinaryPrimitives.ReadUInt16LittleEndian(expected.AsSpan(5)) is var height
                    && IsPixelHeight(projects, height))
                    continue;   // a pixel-device .bin next to the video one

                string? result = "no matching project";
                string? missing = null;
                foreach (var (project, show) in projects)
                {
                    var lines = VideoExport.VideoTimelines(show.Project);
                    IReadOnlyList<Timeline> selected = int.TryParse(name, out int index)
                        ? (index < lines.Count ? [lines[index]] : [])
                        : lines;
                    if (selected.Count == 0) continue;

                    var export = new VideoExport(show.Project, show.Media);
                    missing ??= selected.Take(2).SelectMany(t => t.Items)
                        .FirstOrDefault(i => i.ResourceId is null)?.SourceName;
                    try
                    {
                        byte[] actual = export.Export(selected);
                        if (Environment.GetEnvironmentVariable("PIXELTUNER_DUMP") is { Length: > 0 } dump)
                            File.WriteAllBytes(Path.Combine(dump, $"{Path.GetFileName(dir)}__{name}.bin"), actual);
                        LegacyBinaryAssert.Equal(expected, actual,
                            decodedDifferently: ImagesFrom(expected, selected, WindowsDecodedDifferently));
                        result = null;
                        break;
                    }
                    catch (Exception e)
                    {
                        result = $"{Path.GetFileName(project)}: {e.Message.Split('\n')[0]}";
                    }
                }

                if (result is not null && missing is not null)
                {
                    output.WriteLine($"skip {Rel(root, binPath)}: media missing ({missing}); {result}");
                    continue;
                }
                checkedFiles++;
                if (result is null) output.WriteLine($"ok   {Rel(root, binPath)}");
                else { output.WriteLine($"FAIL {Rel(root, binPath)}: {result}"); failures.Add($"{Rel(root, binPath)}: {result}"); }
            }
        }

        output.WriteLine($"{checkedFiles} video .bin files checked, {failures.Count} failed.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// GIFs that PixelTuner 2 read with slightly different colours. WPF + GDI+ change some pixels
    /// of frames with 16 colours or fewer (checked on Windows against every GIF of the reference
    /// set, 2026-10-21). Studio reads the colours exactly as the palette stores them, which Ivan
    /// chose over parity with Windows; images made from these files may differ in pixel values only.
    /// </summary>
    internal static readonly HashSet<string> WindowsDecodedDifferently = new(StringComparer.OrdinalIgnoreCase)
    {
        "31.gif", "gif-003.gif", "gif-004.gif", "gif-006.gif", "gif-012.gif", "yoLogo.gif",
        "003_fromLeft.gif", "003_steamFromRight.gif", "004_fadeInGreenLights.gif", "transNeft_3shields.gif",
        "002-fadeLeft.gif", "002-fadeRigth.gif", "010 lightning 3.gif", "014 upload_reverse.gif",
        "041l2.gif", "041r2.gif", "042l2.gif", "042r2.gif", "046.gif", "blue fade.gif",
        "glassesHearts5.gif", "0006-hex.gif", "0007-hex.gif", "0008-hex.gif", "e-pic logo.gif",
    };

    /// <summary>
    /// Start addresses of the images whose item (on either channel of a pair) uses one of
    /// <paramref name="files"/>, found by the item's start time.
    /// </summary>
    internal static HashSet<uint> ImagesFrom(byte[] bin, IReadOnlyList<Timeline> selected, HashSet<string> files)
    {
        var result = new HashSet<uint>();
        if (bin.Length < 12) return result;
        int line = (int)BinaryPrimitives.ReadUInt32LittleEndian(bin.AsSpan(12));
        int count = BinaryPrimitives.ReadUInt16LittleEndian(bin.AsSpan(line));
        for (int i = 0; i < count; i++)
        {
            var entry = bin.AsSpan(line + 12 + 12 * i);
            uint address = BinaryPrimitives.ReadUInt32LittleEndian(entry);
            var start = TimeSpan.FromMilliseconds(BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]));
            bool uses = selected.Take(2).SelectMany(t => t.Items)
                .Any(item => item.Start <= start && start < item.End
                    && files.Contains(Path.GetFileName((item.SourceName ?? "").Replace('\\', '/'))));
            if (uses) result.Add(address);
        }
        return result;
    }

    /// <summary>True when the project also has pixel timelines and the .bin is not of a video height.</summary>
    private static bool IsPixelHeight(List<(string Path, Legacy.PtpImportResult Show)> projects, ushort height) =>
        projects.All(p =>
        {
            var lines = VideoExport.VideoTimelines(p.Show.Project);
            var export = new VideoExport(p.Show.Project, p.Show.Media);
            var heights = lines.Select(l => export.Leds(l).Count).ToList();
            bool video = heights.Contains(height) || (heights.Count > 1 && heights[0] + heights[1] == height);
            return !video && p.Show.Project.Timelines.Any(t => t.Kind == TimelineKind.Pixel);
        });

    private static string Rel(string root, string path) => Path.GetRelativePath(root, path);
}
