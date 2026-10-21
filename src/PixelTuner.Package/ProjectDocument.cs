using PixelTuner.Core.Export;
using PixelTuner.Core.Model;

namespace PixelTuner.Package;

/// <summary>
/// The project a window works on: the model, the package its media is read from, where it
/// is saved, and whether there are changes that are not saved yet. UI-free, so the rules
/// about saving are tested without a window.
/// </summary>
public sealed class ProjectDocument : IDisposable
{
    private PxPackage? _source;
    private DateTimeOffset? _createdUtc;

    private ProjectDocument(ShowProject project, string? path, PxPackage? source)
    {
        Project = project;
        Path = path;
        _source = source;
        _createdUtc = source?.Manifest.CreatedUtc;
    }

    public ShowProject Project { get; }

    /// <summary>Where the project is saved; null for a new project or one opened from a stream.</summary>
    public string? Path { get; private set; }

    public bool IsDirty { get; private set; }

    /// <summary>Media files in the package (the ones the project can refer to).</summary>
    public int MediaCount => _source?.Manifest.Resources.Count ?? 0;

    /// <summary>Raised after the name, the path or the dirty flag changed.</summary>
    public event EventHandler? Changed;

    /// <summary>An empty project that is not on disk yet.</summary>
    public static ProjectDocument CreateNew(string name = "") => new(new ShowProject { Name = name }, null, null);

    public static ProjectDocument Open(string path)
    {
        string full = System.IO.Path.GetFullPath(path);
        var package = PxPackage.Open(full);
        return new ProjectDocument(package.Project, full, package);
    }

    /// <summary>Opens from a stream (phones hand over a copy, not a path). The document cannot be saved in place.</summary>
    public static ProjectDocument Open(Stream stream)
    {
        var package = PxPackage.Open(stream, leaveOpen: true);
        return new ProjectDocument(package.Project, null, package);
    }

    /// <summary>For an imported project that was just written to <paramref name="path"/>.</summary>
    public static ProjectDocument OpenImported(string path) => Open(path);

    /// <summary>Media for the .bin export of this project.</summary>
    public ShowMedia? CreateMedia() => _source?.CreateMedia();

    /// <summary>The project name, edited by the user.</summary>
    public string Name
    {
        get => Project.Name;
        set
        {
            if (Project.Name == value) return;
            Project.Name = value;
            MarkChanged();
        }
    }

    /// <summary>Call after every edit of <see cref="Project"/>.</summary>
    public void MarkChanged()
    {
        IsDirty = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Saves over the file the project was opened from or saved to.</summary>
    public void Save()
    {
        if (Path is null) throw new InvalidOperationException("The project has no file yet; use SaveAs.");
        Write(Path);
    }

    public void SaveAs(string path) => Write(System.IO.Path.GetFullPath(path));

    private void Write(string target)
    {
        var writer = new PxPackageWriter();
        if (_source is not null)
            foreach (var resource in _source.Manifest.Resources)
                writer.AddResourceFrom(_source, resource.Id);

        // The target may be the file the media is read from: its reader closes just before the
        // new package replaces it, and the new package is opened again afterwards.
        bool sameFile = _source is not null && Path is not null && SamePath(Path, target);
        var old = _source;
        try
        {
            writer.Save(target, Project, _createdUtc, beforeReplace: sameFile
                ? () => { old!.Dispose(); _source = null; }
                : null);
        }
        catch
        {
            if (sameFile && _source is null && File.Exists(target)) _source = PxPackage.Open(target);   // the old file is still there
            throw;
        }

        var saved = PxPackage.Open(target);
        if (!sameFile) old?.Dispose();
        _source = saved;
        _createdUtc = saved.Manifest.CreatedUtc;
        Path = target;
        IsDirty = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(a, b, OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    public void Dispose()
    {
        _source?.Dispose();
        _source = null;
    }
}
