using PixelTuner.Core.Legacy;

namespace PixelTuner.Core.Tests;

/// <summary>PixelTuner 2 projects imported the way Studio imports them, media next to the project.</summary>
internal static class Shows
{
    public static PtpImportResult Import(string ptpPath)
    {
        var resolver = new PtpMediaResolver(Path.GetDirectoryName(ptpPath)!);
        return PtpImport.Import(PtpDocument.Load(ptpPath), Path.GetFileNameWithoutExtension(ptpPath), name =>
        {
            try { return resolver.Resolve(name); }
            catch (FileNotFoundException) { return null; }
        });
    }
}
