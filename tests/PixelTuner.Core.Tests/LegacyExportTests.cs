using PixelTuner.Core.Legacy;

namespace PixelTuner.Core.Tests;

/// <summary>
/// The .bin from a PixelTuner 2 project must match what PixelTuner 2 wrote to the device.
/// Fixtures are real shows from Fixtures/LegacyProjects: project, media and bin/*.bin.
/// </summary>
public class LegacyExportTests
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "LegacyProjects", name);

    [Theory]
    [InlineData("80_sample", "80_sample.ptp", "0.bin", 80, new int[0])]
    [InlineData("bugi", "60_bugi.ptp", "all.bin", 60, new int[0])]
    [InlineData("36_fan", "36_fans.ptp", "0.bin", 36, new int[0])]
    [InlineData("125_newYear", "125_newYear.ptp", "all.bin", 125, new int[0])]
    [InlineData("150_stavropol", "150_stavrop.ptp", "all.bin", 150, new int[0])]
    [InlineData("150_stavropol", "150_stavrop.ptp", "0.bin", 150, new[] { 0 })]
    [InlineData("150_stavropol", "150_stavrop.ptp", "2.bin", 150, new[] { 2 })]
    [InlineData("150_stavropol", "150_stavrop.ptp", "4.bin", 150, new[] { 4 })]
    [InlineData("cheer_power", "cheer_power.ptp", "0.bin", 125, new[] { 0 })]
    [InlineData("cheer_power", "cheer_power.ptp", "1.bin", 80, new[] { 1 })]
    public void Pixel_export_matches_PixelTuner_2(string folder, string project, string bin, int height, int[] timelines)
    {
        string dir = Fixture(folder);
        var document = PtpDocument.Load(Path.Combine(dir, project));
        var export = new PtpPixelExport(new PtpMediaResolver(dir).Resolve);
        var lines = PtpPixelExport.PixelTimelines(document);
        var selected = timelines.Length == 0 ? lines : timelines.Select(i => lines[i]).ToList();

        byte[] actual = export.Export(selected, (ushort)height);
        byte[] expected = File.ReadAllBytes(Path.Combine(dir, "bin", bin));

        LegacyBinaryAssert.Equal(expected, actual, JpegImages(expected, selected.Where(l => export.TimelineHeight(l) == height).ToList()));
    }

    /// <summary>Addresses of image blocks decoded from JPEG files (gaps shift item indexes).</summary>
    internal static HashSet<uint> JpegImages(byte[] bin, IReadOnlyList<PtpTimeline> lines)
    {
        var result = new HashSet<uint>();
        var entries = LegacyBinaryAssert.ImageEntries(bin).ToList();
        for (int t = 0; t < lines.Count; t++)
        {
            var items = lines[t].Items;
            var addresses = entries.Where(e => e.Timeline == t).Select(e => e.Address).ToList();
            // Walk the items the same way the gaps were inserted.
            int a = items.Count > 0 && items[0].Start != TimeSpan.Zero ? 1 : 0;
            for (int i = 0; i < items.Count && a < addresses.Count; i++)
            {
                if (items[i].FullName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                    || items[i].FullName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                    result.Add(addresses[a]);
                a++;
                if (i + 1 < items.Count && items[i].End != items[i + 1].Start) a++;
            }
        }
        return result;
    }
}
