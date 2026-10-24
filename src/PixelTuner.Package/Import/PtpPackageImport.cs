using PixelTuner.Core.Legacy;

namespace PixelTuner.Package.Import;

/// <summary>
/// Turns a PixelTuner 2 project (.ptp) into a .pxpkg with all its media. Files the project
/// names are looked for next to the project (PixelTuner 2 paths are often absolute paths on
/// the author's machine). Whatever is not found is listed in <see cref="PtpImportResult.Missing"/>:
/// the app asks the user once, then imports again with the answers as replacements or
/// folders to search.
/// </summary>
public static class PtpPackageImport
{
    /// <param name="replacements">Files the user picked for missing ones, by the name the project uses.</param>
    /// <param name="searchFolders">More folders to look in by file name, for example one the user picked.</param>
    public static PtpImportResult Import(string ptpPath,
        IReadOnlyDictionary<string, string>? replacements = null,
        IEnumerable<string>? searchFolders = null)
    {
        var document = PtpDocument.Load(ptpPath);
        var resolvers = new List<PtpMediaResolver> { new(Path.GetDirectoryName(Path.GetFullPath(ptpPath))!) };
        resolvers.AddRange((searchFolders ?? []).Select(f => new PtpMediaResolver(f)));

        return PtpImport.Import(document, Path.GetFileNameWithoutExtension(ptpPath), name =>
        {
            if (replacements is not null && replacements.TryGetValue(name, out var replacement) && File.Exists(replacement))
                return replacement;
            foreach (var resolver in resolvers)
                if (resolver.TryResolve(name) is { } path) return path;
            return null;
        });
    }

    /// <summary>Writes the imported show and its media to a .pxpkg.</summary>
    public static void Save(PtpImportResult imported, string packagePath)
    {
        var writer = new PxPackageWriter();
        foreach (var entry in imported.Media.Entries)
        {
            string id = entry.FilePath is { } path ? writer.AddMediaFile(path) : writer.AddMedia(entry.Content!, entry.Name);
            if (id != entry.Id)
                throw new InvalidOperationException($"Media '{entry.Name}' changed while the show was imported.");
        }
        writer.Save(packagePath, imported.Project);
    }
}
