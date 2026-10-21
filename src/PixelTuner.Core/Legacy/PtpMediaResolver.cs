namespace PixelTuner.Core.Legacy;

/// <summary>
/// Finds the media files of a PixelTuner 2 project. Projects store either paths relative
/// to the project ("./a.bmp") or absolute Windows paths from the author's machine.
/// When an absolute path does not exist, the file is looked up by name next to the project,
/// which is where PixelTuner 2's "Export project" put it.
/// </summary>
public sealed class PtpMediaResolver(string projectDirectory)
{
    public string Resolve(string fullName)
    {
        string normalized = fullName.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        bool windowsAbsolute = fullName.Length > 2 && fullName[1] == ':';

        if (!windowsAbsolute)
        {
            string relative = Path.GetFullPath(Path.Combine(projectDirectory, normalized));
            if (FindIgnoringCase(relative) is { } found) return found;
        }
        else if (File.Exists(fullName))
        {
            return fullName;
        }

        string fileName = Path.GetFileName(normalized);
        if (FindIgnoringCase(Path.Combine(projectDirectory, fileName)) is { } nearby) return nearby;

        throw new FileNotFoundException($"Media file '{fullName}' of the project was not found.", fullName);
    }

    /// <summary>Windows paths are case-insensitive; macOS and Linux file systems may not be.</summary>
    private static string? FindIgnoringCase(string path)
    {
        if (File.Exists(path)) return path;
        string? directory = Path.GetDirectoryName(path);
        if (directory is null || !Directory.Exists(directory)) return null;
        string name = Path.GetFileName(path);
        return Directory.EnumerateFiles(directory)
            .FirstOrDefault(f => string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase));
    }
}
