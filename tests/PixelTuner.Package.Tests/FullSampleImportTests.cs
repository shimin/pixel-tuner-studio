using PixelTuner.Core.Legacy;
using PixelTuner.Package.Import;
using Xunit.Abstractions;

namespace PixelTuner.Package.Tests;

/// <summary>
/// Every project of the PixelTuner 2 reference set (release "samples-v1", unpacked into
/// PIXELTUNER_SAMPLES) is imported, and its package must export the same .bin for every
/// timeline as the project itself.
/// </summary>
public class FullSampleImportTests(ITestOutputHelper output)
{
    [Fact]
    public void Every_project_in_the_reference_set_imports_without_loss()
    {
        string? root = Environment.GetEnvironmentVariable("PIXELTUNER_SAMPLES");
        if (string.IsNullOrEmpty(root))
        {
            output.WriteLine("PIXELTUNER_SAMPLES is not set, nothing to check.");
            return;
        }

        string temp = Directory.CreateTempSubdirectory("ptp-import-samples").FullName;
        var failures = new List<string>();
        int projects = 0, exports = 0;
        try
        {
            foreach (string ptp in Directory.EnumerateFiles(root, "*.ptp", SearchOption.AllDirectories).Order())
            {
                string name = Path.GetRelativePath(root, ptp);
                string package = Path.Combine(temp, $"{projects}{PxPackageFormat.Extension}");
                try
                {
                    var result = PtpImporter.Import(ptp, package);
                    using var pkg = PxPackage.Open(package);
                    Assert.Empty(pkg.VerifyResources());
                    int compared = ExportComparison.Compare(PtpDocument.Load(ptp), Path.GetDirectoryName(ptp)!, pkg);
                    exports += compared;
                    output.WriteLine($"ok   {name}: {pkg.Manifest.Resources.Count} media, {compared} exports"
                        + (result.MissingMedia.Count > 0 ? $", missing {string.Join(", ", result.MissingMedia)}" : ""));
                }
                catch (Exception e)
                {
                    output.WriteLine($"FAIL {name}: {e.Message.Split('\n')[0]}");
                    failures.Add($"{name}: {e.Message.Split('\n')[0]}");
                }
                finally
                {
                    File.Delete(package);
                }
                projects++;
            }
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }

        output.WriteLine($"{projects} projects imported, {exports} exports compared, {failures.Count} failed.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
