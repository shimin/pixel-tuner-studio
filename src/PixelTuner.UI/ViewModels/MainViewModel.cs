using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelTuner.Package;
using PixelTuner.Package.Import;
using PixelTuner.UI.Resources;
using PixelTuner.UI.Services;

namespace PixelTuner.UI.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    /// <summary>Set by the view: file dialogs need the window or view that hosts them.</summary>
    public IFilePicker? FilePicker { get; set; }

    /// <summary>Set by the view on desktop. Without it nothing asks: changes are discarded.</summary>
    public IDialogService? Dialogs { get; set; }

    /// <summary>Raised when the user chose Exit; the window closes (and asks about unsaved changes on the way).</summary>
    public event EventHandler? ExitRequested;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProject), nameof(Title), nameof(ProjectName), nameof(Project), nameof(UndoHeader), nameof(RedoHeader))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(SaveAsCommand), nameof(CloseProjectCommand), nameof(UndoCommand), nameof(RedoCommand))]
    ProjectDocument? _document;

    /// <summary>The timelines of the open project. Studio only; Stage opens projects to look at them.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEditor), nameof(ShowStart))]
    EditorViewModel? _editor;

    public bool ShowEditor => Editor is not null;
    public bool ShowStart => Editor is null;

    [ObservableProperty]
    string? _error;

    [ObservableProperty]
    string? _status;

    public bool HasProject => Document is not null;

    /// <summary>Studio edits and saves; Stage only opens.</summary>
    public bool CanEdit => Product.Current.CanEdit;

    /// <summary>The window menu has Exit and About on Windows; macOS has them in the app menu.</summary>
    public bool ShowWindowsMenus => !OperatingSystem.IsMacOS();

    /// <summary>The app name, for the heading of the start screen.</summary>
    public string AppName => Product.Current.Name;

    public string AboutHeader => string.Format(Strings.Command_About, Product.Current.Name);

    /// <summary>
    /// The window title: the project, a mark for unsaved changes, and the app. Windows marks
    /// changes with an asterisk, macOS with "Edited".
    /// </summary>
    public string Title
    {
        get
        {
            if (Document is not { } doc) return Product.Current.Name;
            string mark = !doc.IsDirty ? "" : OperatingSystem.IsMacOS() ? " — " + Strings.Document_Edited : "*";
            return $"{DocumentName}{mark} — {Product.Current.Name}";
        }
    }

    /// <summary>What the user calls the project: its name, else the file, else "Untitled".</summary>
    public string DocumentName
    {
        get
        {
            if (Document is not { } doc) return "";
            if (!string.IsNullOrWhiteSpace(doc.Name)) return doc.Name;
            return doc.Path is { } path ? System.IO.Path.GetFileNameWithoutExtension(path) : Strings.Document_Untitled;
        }
    }

    public string ProjectName
    {
        get => Document?.Name ?? "";
        set
        {
            if (Document is { } doc) doc.Name = value;
        }
    }

    public ProjectSummary? Project => Document is null ? null : ProjectSummary.From(Document);

    partial void OnDocumentChanging(ProjectDocument? value)
    {
        if (Document is { } old) old.Changed -= DocumentEdited;
    }

    partial void OnDocumentChanged(ProjectDocument? value)
    {
        if (value is not null) value.Changed += DocumentEdited;
        var old = Editor;
        Editor = value is not null && CanEdit ? new EditorViewModel(value, AudioEngine.Current, () => FilePicker) : null;
        old?.Dispose();
    }

    // Saving runs on a pool thread and reports from there; bindings may only be touched on the UI thread.
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;

    private void DocumentEdited(object? sender, EventArgs e)
    {
        if (_ui is null || SynchronizationContext.Current == _ui) RefreshFromDocument();
        else _ui.Post(_ => RefreshFromDocument(), null);
    }

    private void RefreshFromDocument()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(Project));
        OnPropertyChanged(nameof(UndoHeader));
        OnPropertyChanged(nameof(RedoHeader));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    /// <summary>"Undo" with what would be undone: "Undo Rename project".</summary>
    public string UndoHeader => Header(Strings.Command_Undo, Strings.Command_UndoNamed, Document?.UndoKey);

    public string RedoHeader => Header(Strings.Command_Redo, Strings.Command_RedoNamed, Document?.RedoKey);

    private static string Header(string plain, string named, string? editKey)
    {
        string? what = editKey is null ? null : Strings.ResourceManager.GetString("Edit_" + editKey, Strings.Culture);
        return what is null ? plain : string.Format(named, what);
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    void Undo() => Document?.Undo();

    [RelayCommand(CanExecute = nameof(CanRedo))]
    void Redo() => Document?.Redo();

    private bool CanUndo => Document?.CanUndo ?? false;
    private bool CanRedo => Document?.CanRedo ?? false;

    [RelayCommand]
    async Task NewProjectAsync()
    {
        if (!await ConfirmDiscardAsync()) return;
        Error = Status = null;
        SetDocument(ProjectDocument.CreateNew());
    }

    [RelayCommand]
    async Task OpenProjectAsync()
    {
        if (FilePicker is null) return;
        if (!CanEdit)
        {
            await OpenFromStreamAsync();
            return;
        }
        if (!await ConfirmDiscardAsync()) return;
        string? path = await FilePicker.PickPackagePathAsync();
        if (path is not null) await OpenCoreAsync(path);
    }

    /// <summary>Opens a file that came from outside the menu: dropped on the window, or double-clicked.</summary>
    public async Task OpenPathAsync(string path)
    {
        if (!CanEdit || !await ConfirmDiscardAsync()) return;
        await OpenCoreAsync(path);
    }

    private async Task OpenCoreAsync(string path)
    {
        Error = Status = null;
        try
        {
            SetDocument(await Task.Run(() => ProjectDocument.Open(path)));
        }
        catch (Exception ex) when (IsFileProblem(ex))
        {
            Error = string.Format(Strings.Error_OpenFailed, ex.Message);
        }
    }

    private async Task OpenFromStreamAsync()
    {
        Error = Status = null;
        try
        {
            await using var stream = await FilePicker!.PickPackageAsync();
            if (stream is null) return;
            SetDocument(ProjectDocument.Open(stream));
        }
        catch (Exception ex) when (IsFileProblem(ex))
        {
            Error = string.Format(Strings.Error_OpenFailed, ex.Message);
        }
    }

    [RelayCommand]
    async Task ImportLegacyProjectAsync()
    {
        if (FilePicker is null || !CanEdit || !await ConfirmDiscardAsync()) return;
        Error = Status = null;
        try
        {
            string? ptp = await FilePicker.PickLegacyProjectAsync();
            if (ptp is null) return;
            string? target = await FilePicker.PickPackageSavePathAsync(
                System.IO.Path.GetFileNameWithoutExtension(ptp) + PxPackageFormat.Extension);
            if (target is null) return;

            var result = await Task.Run(() => PtpImporter.Import(ptp, target));
            SetDocument(await Task.Run(() => ProjectDocument.OpenImported(target)));
            Status = result.MissingMedia.Count == 0
                ? Strings.Import_Done
                : string.Format(Strings.Import_DoneMissing, result.MissingMedia.Count,
                    string.Join(", ", result.MissingMedia.Select(System.IO.Path.GetFileName)));
        }
        catch (Exception ex) when (IsFileProblem(ex) || ex is System.Xml.XmlException or NotSupportedException)
        {
            Error = string.Format(Strings.Error_ImportFailed, ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(HasProject))]
    async Task SaveAsync() => await TrySaveAsync();

    [RelayCommand(CanExecute = nameof(HasProject))]
    async Task SaveAsAsync() => await TrySaveAsAsync();

    [RelayCommand(CanExecute = nameof(HasProject))]
    async Task CloseProjectAsync()
    {
        if (!await ConfirmDiscardAsync()) return;
        Error = Status = null;
        SetDocument(null);
    }

    [RelayCommand]
    void Exit() => ExitRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    async Task AboutAsync()
    {
        if (Dialogs is not null) await Dialogs.ShowAboutAsync(Product.Current.Name, Product.Version);
    }

    /// <summary>
    /// Asks what to do with unsaved changes before they would be lost. True when the caller
    /// may go on (nothing to lose, saved, or discarded); false when the user cancelled.
    /// </summary>
    public async Task<bool> ConfirmDiscardAsync()
    {
        if (Document is not { IsDirty: true }) return true;
        if (Dialogs is null) return true;
        return await Dialogs.AskSaveChangesAsync(DocumentName) switch
        {
            SaveChoice.Save => await TrySaveAsync(),
            SaveChoice.Discard => true,
            _ => false,
        };
    }

    /// <summary>Saves; asks for a file when there is none. False when cancelled or it failed.</summary>
    private async Task<bool> TrySaveAsync() =>
        Document is { Path: not null } ? await RunSaveAsync(doc => doc.Save()) : await TrySaveAsAsync();

    private async Task<bool> TrySaveAsAsync()
    {
        if (Document is not { } doc || FilePicker is null) return false;
        string suggested = (string.IsNullOrWhiteSpace(DocumentName) ? Strings.Document_Untitled : DocumentName) + PxPackageFormat.Extension;
        string? path = await FilePicker.PickPackageSavePathAsync(suggested);
        return path is not null && await RunSaveAsync(d => d.SaveAs(path));
    }

    private async Task<bool> RunSaveAsync(Action<ProjectDocument> save)
    {
        var doc = Document!;
        Error = Status = null;
        try
        {
            await Task.Run(() => save(doc));
            return true;
        }
        catch (Exception ex) when (IsFileProblem(ex))
        {
            Error = string.Format(Strings.Error_SaveFailed, ex.Message);
            return false;
        }
    }

    private void SetDocument(ProjectDocument? document)
    {
        var old = Document;
        Document = document;
        old?.Dispose();
    }

    private static bool IsFileProblem(Exception ex) =>
        ex is InvalidDataException or PackageTooNewException or IOException or UnauthorizedAccessException or JsonException;

    public void Dispose() => SetDocument(null);
}

/// <summary>What the start screen shows about an opened package.</summary>
public sealed record ProjectSummary(int Timelines, int Devices, int Markers, int Media, ObservableCollection<string> TimelineNames)
{
    public static ProjectSummary From(ProjectDocument doc) => new(
        doc.Project.Timelines.Count,
        doc.Project.Devices.Count,
        doc.Project.Markers.Count,
        doc.MediaCount,
        new ObservableCollection<string>(doc.Project.Timelines.Select(t => t.Name)));
}
