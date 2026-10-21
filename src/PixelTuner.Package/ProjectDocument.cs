using PixelTuner.Core.Editing;
using PixelTuner.Core.Export;
using PixelTuner.Core.Imaging;
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

    private ProjectDocument(ShowProject project, string? path, PxPackage? source, TimeProvider? time)
    {
        Project = project;
        Path = path;
        _source = source;
        // Imported projects and older packages do not carry the picture sizes the editor's rules need.
        // Reading them is not a change of the project; they are saved with the next save.
        if (source is not null) MediaSizes.Fill(project, id => source.HasResource(id) ? source.OpenResource(id) : null);
        _createdUtc = source?.Manifest.CreatedUtc;
        _history = new UndoHistory(time);
        _history.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    private readonly UndoHistory _history;

    // Media added since the package was opened or saved (the soundtrack), by id. They are written on save.
    private readonly Dictionary<string, (byte[] Content, string Name)> _added = new(StringComparer.Ordinal);

    public ShowProject Project { get; }

    /// <summary>Where the project is saved; null for a new project or one opened from a stream.</summary>
    public string? Path { get; private set; }

    /// <summary>True when the project differs from what was last saved. Undoing back to the saved state clears it.</summary>
    public bool IsDirty => _history.IsDirty;

    public bool CanUndo => _history.CanUndo;
    public bool CanRedo => _history.CanRedo;

    /// <summary>Kind of change that undo would revert ("RenameProject"), for the menu label.</summary>
    public string? UndoKey => _history.UndoKey;

    public string? RedoKey => _history.RedoKey;

    /// <summary>Media files in the package (the ones the project can refer to).</summary>
    public int MediaCount => (_source?.Manifest.Resources.Count ?? 0) + _added.Count(a => !(_source?.HasResource(a.Key) ?? false));

    /// <summary>Raised after any edit, undo, redo or save, and when the path changes.</summary>
    public event EventHandler? Changed;

    /// <summary>An empty project that is not on disk yet.</summary>
    public static ProjectDocument CreateNew(string name = "", TimeProvider? time = null) =>
        new(new ShowProject { Name = name }, null, null, time);

    public static ProjectDocument Open(string path, TimeProvider? time = null)
    {
        string full = System.IO.Path.GetFullPath(path);
        var package = PxPackage.Open(full);
        return new ProjectDocument(package.Project, full, package, time);
    }

    /// <summary>Opens from a stream (phones hand over a copy, not a path). The document cannot be saved in place.</summary>
    public static ProjectDocument Open(Stream stream, TimeProvider? time = null)
    {
        var package = PxPackage.Open(stream, leaveOpen: true);
        return new ProjectDocument(package.Project, null, package, time);
    }

    /// <summary>For an imported project that was just written to <paramref name="path"/>.</summary>
    public static ProjectDocument OpenImported(string path, TimeProvider? time = null) => Open(path, time);

    /// <summary>Media for the .bin export of this project.</summary>
    public ShowMedia? CreateMedia() => _source?.CreateMedia();

    /// <summary>
    /// Takes a file into the project and returns its resource id (its SHA-256). The file is read now, so it
    /// may be moved or deleted afterwards; it is written into the package on the next save.
    /// </summary>
    public string AddMedia(string filePath)
    {
        byte[] content = File.ReadAllBytes(filePath);
        string id = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(content));
        if (!(_source?.HasResource(id) ?? false)) _added[id] = (content, System.IO.Path.GetFileName(filePath));
        return id;
    }

    /// <summary>Makes a sound file the soundtrack (an undoable change).</summary>
    public void SetAudio(string filePath) => Execute(Edits.SetAudio(Project, AddMedia(filePath), filePath));

    public void RemoveAudio() => Execute(Edits.RemoveAudio(Project));

    public bool HasMedia(string? id) => id is not null && (_added.ContainsKey(id) || (_source?.HasResource(id) ?? false));

    /// <summary>File name of a media file as the user added it ("song.mp3"), or null when unknown.</summary>
    public string? MediaName(string? id)
    {
        if (id is null) return null;
        if (_added.TryGetValue(id, out var added)) return added.Name;
        return _source?.HasResource(id) == true ? _source.GetResource(id).OriginalName : null;
    }

    /// <summary>Opens a media file for reading; the caller disposes the stream. Null when the project has no such media.</summary>
    public Stream? OpenMedia(string? id)
    {
        if (id is null) return null;
        if (_added.TryGetValue(id, out var added)) return new MemoryStream(added.Content, writable: false);
        if (_source?.HasResource(id) != true) return null;
        // Package entries cannot seek and are read one at a time: hand out a copy.
        return new MemoryStream(_source.ReadResource(id), writable: false);
    }

    /// <summary>The project name, edited by the user (an undoable edit).</summary>
    public string Name
    {
        get => Project.Name;
        set
        {
            if (Project.Name != value) Execute(Edits.RenameProject(Project, value));
        }
    }

    /// <summary>
    /// Makes a change to the project, as built by <see cref="Edits"/>. Every change of the
    /// model goes through here, so it can be undone and marks the project as changed.
    /// </summary>
    public void Execute(Edit edit) => _history.Do(edit);

    public bool Undo() => _history.Undo();
    public bool Redo() => _history.Redo();

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
        // New media is written only if the project still refers to it (undo may have taken it back).
        var written = new List<string>();
        foreach (var (id, media) in _added)
            if (IsReferenced(id))
            {
                writer.AddMedia(media.Content, media.Name);
                written.Add(id);
            }

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
        // Written media is in the package now. Media that undo took back stays here, so redo can bring it back.
        foreach (var id in written) _added.Remove(id);
        if (!sameFile) old?.Dispose();
        _source = saved;
        _createdUtc = saved.Manifest.CreatedUtc;
        Path = target;
        _history.MarkSaved();   // raises Changed
    }

    private bool IsReferenced(string id) =>
        Project.AudioResourceId == id || Project.CanvasResourceId == id
        || Project.Timelines.Any(t => t.Items.Any(i => i.ResourceId == id));

    private static bool SamePath(string a, string b) =>
        string.Equals(a, b, OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    public void Dispose()
    {
        _source?.Dispose();
        _source = null;
    }
}
