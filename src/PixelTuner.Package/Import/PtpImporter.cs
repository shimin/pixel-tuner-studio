using PixelTuner.Core.Legacy;
using PixelTuner.Core.Model;

namespace PixelTuner.Package.Import;

/// <summary>
/// Imports a PixelTuner 2 project (.ptp) into a .pxpkg: the project becomes a
/// <see cref="ShowProject"/>, and the soundtrack, images, GIFs and the canvas drawing go
/// into the package by content hash, so the package opens on any platform without the
/// original folders.
/// </summary>
public static class PtpImporter
{
    /// <summary>Imports <paramref name="ptpPath"/> and saves the package to <paramref name="packagePath"/>.</summary>
    public static PtpImportResult Import(string ptpPath, string packagePath, PtpImportOptions? options = null)
    {
        var (project, writer, missing) = Prepare(ptpPath, options);
        writer.Save(packagePath, project);
        return new PtpImportResult(project, missing);
    }

    /// <summary>Imports into a stream (for example, a file the platform's document picker opened).</summary>
    public static PtpImportResult Import(string ptpPath, Stream output, PtpImportOptions? options = null)
    {
        var (project, writer, missing) = Prepare(ptpPath, options);
        writer.Write(output, project);
        return new PtpImportResult(project, missing);
    }

    private static (ShowProject Project, PxPackageWriter Writer, IReadOnlyList<string> Missing) Prepare(
        string ptpPath, PtpImportOptions? options)
    {
        var document = PtpDocument.Load(ptpPath);
        string directory = Path.GetDirectoryName(Path.GetFullPath(ptpPath))!;
        var resolver = new PtpMediaResolver(directory);
        var writer = new PxPackageWriter();

        var converter = new PtpConverter(document, path =>
        {
            string? file = TryResolve(resolver, path) ?? options?.FindMissing?.Invoke(path);
            return file is not null && File.Exists(file) ? writer.AddMediaFile(file) : null;
        });
        var project = converter.ToProject(Path.GetFileNameWithoutExtension(ptpPath), writer.AddMedia);
        return (project, writer, converter.MissingMedia.ToList());
    }

    private static string? TryResolve(PtpMediaResolver resolver, string path)
    {
        try { return resolver.Resolve(path); }
        catch (FileNotFoundException) { return null; }
    }
}

public sealed class PtpImportOptions
{
    /// <summary>
    /// Asked once for each stored media path that is not where the project says (nor next to
    /// it): the stored path in, a file to use instead out, or null to import without it.
    /// This replaces PixelTuner 2's "fix missing files" dialog.
    /// </summary>
    public Func<string, string?>? FindMissing { get; init; }
}

/// <param name="Project">The project as saved in the package.</param>
/// <param name="MissingMedia">Stored paths of the files that were imported without their media.</param>
public sealed record PtpImportResult(ShowProject Project, IReadOnlyList<string> MissingMedia);
