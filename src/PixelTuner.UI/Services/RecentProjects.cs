namespace PixelTuner.UI.Services;

/// <summary>
/// The projects opened or saved last, newest first, for the start screen. Kept in a text file (one path a line) in the user's
/// application data, so it needs no serializer and no platform code; without a file the list lives in memory only (tests, phones).
/// A path that no longer exists is dropped when the list is read.
/// </summary>
public sealed class RecentProjects
{
    public const int DefaultLimit = 10;

    private static readonly StringComparer PathComparer = OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    private readonly string? _file;
    private readonly int _limit;
    private readonly List<string> _paths = [];

    /// <param name="file">Where the list is kept; null keeps it in memory.</param>
    public RecentProjects(string? file = null, int limit = DefaultLimit)
    {
        _file = file;
        _limit = limit;
        Load();
    }

    /// <summary>The file of the app on this computer.</summary>
    public static string DefaultFile(string appName) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName, "recent-projects.txt");

    public IReadOnlyList<string> Paths => _paths;

    public event EventHandler? Changed;

    /// <summary>Puts a project on top of the list (or moves it there).</summary>
    public void Add(string path)
    {
        path = Path.GetFullPath(path);
        _paths.RemoveAll(p => PathComparer.Equals(p, path));
        _paths.Insert(0, path);
        if (_paths.Count > _limit) _paths.RemoveRange(_limit, _paths.Count - _limit);
        Save();
    }

    public void Remove(string path)
    {
        if (_paths.RemoveAll(p => PathComparer.Equals(p, path)) > 0) Save();
    }

    public void Clear()
    {
        if (_paths.Count == 0) return;
        _paths.Clear();
        Save();
    }

    private void Load()
    {
        if (_file is null || !File.Exists(_file)) return;
        try
        {
            foreach (string line in File.ReadAllLines(_file))
                if (line.Length > 0 && File.Exists(line) && !_paths.Contains(line, PathComparer) && _paths.Count < _limit) _paths.Add(line);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A list that cannot be read is an empty one.
        }
    }

    private void Save()
    {
        if (_file is not null)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
                string temp = _file + ".tmp";
                File.WriteAllLines(temp, _paths);
                File.Move(temp, _file, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The list is a convenience: not being able to keep it must not stop a project from opening.
            }
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
