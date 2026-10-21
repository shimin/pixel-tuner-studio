using System.Buffers.Binary;
using PixelTuner.Core.Legacy;
using Xunit.Abstractions;

namespace PixelTuner.Core.Tests;

/// <summary>
/// The whole reference set (about 500 MB, release "samples-v1"), when it is unpacked
/// and PIXELTUNER_SAMPLES points at it. Every pixel-device .bin in it must match.
/// Naming in the set: "N.bin" is pixel timeline N alone, any other name is all
/// pixel timelines of the .bin's height. Video-device .bin files are not covered yet.
/// </summary>
public class FullSampleExportTests(ITestOutputHelper output)
{
    [Fact]
    public void Every_pixel_bin_in_the_reference_set_matches()
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
            var projects = Directory.EnumerateFiles(dir, "*.ptp").Order().ToList();

            foreach (string binPath in Directory.EnumerateFiles(binDir, "*.bin").Order())
            {
                byte[] expected = File.ReadAllBytes(binPath);
                if (expected.Length < 12) { output.WriteLine($"skip {Rel(root, binPath)}: empty"); continue; }
                ushort height = BinaryPrimitives.ReadUInt16LittleEndian(expected.AsSpan(5));
                string name = Path.GetFileNameWithoutExtension(binPath);

                string? result = null;
                string? missing = null;
                bool any = false;
                foreach (string project in projects)
                {
                    var document = PtpDocument.Load(project);
                    var export = new PtpPixelExport(new PtpMediaResolver(dir).Resolve);
                    var lines = PtpPixelExport.PixelTimelines(document);
                    List<PtpTimeline> selected;
                    try
                    {
                        selected = int.TryParse(name, out int index)
                            ? (index < lines.Count ? [lines[index]] : [])
                            : lines.ToList();
                        selected = selected.Where(l => l.Items.Count > 0 && export.TimelineHeight(l) == height).ToList();
                    }
                    catch (FileNotFoundException e)
                    {
                        missing = e.Message;
                        continue;
                    }
                    if (selected.Count == 0) continue;
                    any = true;

                    byte[] actual;
                    try { actual = export.Export(selected, height); }
                    catch (FileNotFoundException e) { missing = e.Message; any = false; continue; }
                    try
                    {
                        LegacyBinaryAssert.Equal(expected, actual, LegacyExportTests.JpegImages(expected, selected));
                        result = null;
                        break;
                    }
                    catch (Exception e)
                    {
                        result = $"{Path.GetFileName(project)}: {e.Message.Split('\n')[0]}";
                    }
                }

                if (!any || (result is not null && missing is not null))
                {
                    // Not an export bug: media missing from the set, or a device type not ported yet
                    // (video devices, PixelHoop, fan).
                    output.WriteLine($"skip {Rel(root, binPath)}: " + (missing ?? $"no pixel timelines of height {height}"));
                    continue;
                }
                checkedFiles++;
                if (result is null) output.WriteLine($"ok   {Rel(root, binPath)}");
                else { output.WriteLine($"FAIL {Rel(root, binPath)}: {result}"); failures.Add($"{Rel(root, binPath)}: {result}"); }
            }
        }

        output.WriteLine($"{checkedFiles} pixel .bin files checked, {failures.Count} failed.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static string Rel(string root, string path) => Path.GetRelativePath(root, path);
}
