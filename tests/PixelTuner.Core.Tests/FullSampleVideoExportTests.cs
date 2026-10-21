using System.Buffers.Binary;
using PixelTuner.Core.Legacy;
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
                .Select(p => (Path: p, Document: PtpDocument.Load(p)))
                .Where(p => PtpVideoExport.VideoTimelines(p.Document).Count > 0)
                .ToList();
            if (projects.Count == 0) continue;
            var resolver = new PtpMediaResolver(dir);

            foreach (string binPath in Directory.EnumerateFiles(binDir, "*.bin").Order())
            {
                byte[] expected = File.ReadAllBytes(binPath);
                string name = Path.GetFileNameWithoutExtension(binPath);
                if (expected.Length >= 12 && BinaryPrimitives.ReadUInt16LittleEndian(expected.AsSpan(5)) is var height
                    && IsPixelHeight(projects, height))
                    continue;   // a pixel-device .bin next to the video one

                string? result = "no matching project";
                string? missing = null;
                foreach (var (project, document) in projects)
                {
                    var lines = PtpVideoExport.VideoTimelines(document);
                    IReadOnlyList<PtpTimeline> selected = int.TryParse(name, out int index)
                        ? (index < lines.Count ? [lines[index]] : [])
                        : lines;
                    if (selected.Count == 0) continue;

                    var export = new PtpVideoExport(document, file =>
                    {
                        try { return resolver.Resolve(file); }
                        catch (FileNotFoundException) { missing ??= file; return null; }
                    });
                    try
                    {
                        byte[] actual = export.Export(selected);
                        if (Environment.GetEnvironmentVariable("PIXELTUNER_DUMP") is { Length: > 0 } dump)
                            File.WriteAllBytes(Path.Combine(dump, $"{Path.GetFileName(dir)}__{name}.bin"), actual);
                        LegacyBinaryAssert.Equal(expected, actual);
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

    /// <summary>True when the project also has pixel timelines and the .bin is not of a video height.</summary>
    private static bool IsPixelHeight(List<(string Path, PtpDocument Document)> projects, ushort height) =>
        projects.All(p =>
        {
            var lines = PtpVideoExport.VideoTimelines(p.Document);
            var export = new PtpVideoExport(p.Document, _ => null);
            var heights = lines.Select(l => export.Leds(l).Count).ToList();
            bool video = heights.Contains(height) || (heights.Count > 1 && heights[0] + heights[1] == height);
            return !video && p.Document.Timelines.Any(t => t.Type == PtpTimeline.PixelType);
        });

    private static string Rel(string root, string path) => Path.GetRelativePath(root, path);
}
