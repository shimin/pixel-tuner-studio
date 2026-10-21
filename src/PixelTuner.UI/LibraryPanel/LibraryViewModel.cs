using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelTuner.Core.Editing;
using PixelTuner.Core.Imaging;
using PixelTuner.Core.Model;
using PixelTuner.Package;
using PixelTuner.UI.Resources;
using PixelTuner.UI.Services;
using PixelTuner.UI.Timelines;
using PixelTuner.UI.ViewModels;

namespace PixelTuner.UI.LibraryPanel;

/// <summary>One thing of the library as the panel shows it.</summary>
public sealed class LibraryEntryViewModel(LibraryEntry entry, PlacementProblem problem, Func<LibraryEntry, Bitmap?> thumbnail)
{
    private Bitmap? _thumbnail;
    private bool _thumbnailMade;

    public LibraryEntry Entry { get; } = entry;
    public string Name => Entry.Name;

    /// <summary>What the entry cannot be put on the selected timeline for; None when it can.</summary>
    public PlacementProblem Problem { get; } = problem;

    public bool CanPlace => Problem == PlacementProblem.None;

    /// <summary>What cannot be put on the selected timeline is shown dimmed.</summary>
    public double Dim => CanPlace ? 1 : 0.45;
    public bool IsMissing => Library.HasMedia(Entry.Kind) && Entry.ResourceId is null;

    /// <summary>Size, frames or values: "40 × 36", "12 frames", "#FF0000".</summary>
    public string Detail => Entry.Kind switch
    {
        LibraryKind.Video => Entry.Frames > 0 ? string.Format(Strings.Library_VideoDetail, Entry.Width, Entry.Height, Entry.Frames) : $"{Entry.Width} × {Entry.Height}",
        LibraryKind.Color => Entry.Color ?? "",
        LibraryKind.Effect => $"{Entry.Color} → {Entry.ColorLast}",
        _ => $"{Entry.Width} × {Entry.Height}",
    };

    public string Reason => Problem switch
    {
        PlacementProblem.WrongKind => Strings.Library_WrongKind,
        PlacementProblem.WrongHeight => string.Format(Strings.Library_WrongHeight, Entry.Height),
        PlacementProblem.MissingMedia => Strings.Library_Missing,
        _ => "",
    };

    /// <summary>The picture (the first frame of a GIF); null for colours and effects, and when the file is missing or damaged.</summary>
    public Bitmap? Thumbnail
    {
        get
        {
            if (!_thumbnailMade)
            {
                _thumbnailMade = true;
                _thumbnail = thumbnail(Entry);
            }
            return _thumbnail;
        }
    }

    /// <summary>The colour of a colour entry, the two colours of an effect as a gradient.</summary>
    public IBrush? Swatch
    {
        get
        {
            try
            {
                if (Entry.Kind == LibraryKind.Color) return Brush(Entry.Color);
                if (Entry.Kind != LibraryKind.Effect) return null;
                return new LinearGradientBrush
                {
                    StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
                    EndPoint = new Avalonia.RelativePoint(1, 0, Avalonia.RelativeUnit.Relative),
                    GradientStops = [new GradientStop(Color(Entry.Color), 0), new GradientStop(Color(Entry.ColorLast), 1)],
                };
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }
    }

    private static Avalonia.Media.Color Color(string? text)
    {
        var c = ColorText.ParseRgb(text);
        return Avalonia.Media.Color.FromRgb(c.R, c.G, c.B);
    }

    private static IBrush Brush(string? text) => new SolidColorBrush(Color(text));
}

/// <summary>
/// The library panel of the editor: what the project has to put on timelines, narrowed to what the selected timeline
/// can play. A thing is offered only where it can be played, so what goes on a timeline always builds into the .bin.
/// </summary>
public sealed partial class LibraryViewModel : ObservableObject
{
    private readonly EditorViewModel _editor;
    private readonly Func<IFilePicker?> _picker;
    private readonly Dictionary<string, Bitmap?> _thumbnails = [];

    public LibraryViewModel(EditorViewModel editor, Func<IFilePicker?> picker)
    {
        _editor = editor;
        _picker = picker;
        Document.Changed += (_, _) => Rebuild();
        _editor.Timelines.SelectionChanged += (_, _) => Rebuild();
        Rebuild();
    }

    private ProjectDocument Document => _editor.Document;

    /// <summary>What the panel lists now.</summary>
    public ObservableCollection<LibraryEntryViewModel> Entries { get; } = [];

    /// <summary>Entries whose file is missing: the panel lists them apart, with a button to find the file.</summary>
    public ObservableCollection<LibraryEntryViewModel> Missing { get; } = [];

    [ObservableProperty] private LibraryEntryViewModel? _selected;
    [ObservableProperty] private bool _showAll;
    [ObservableProperty] private string _newColor = "#FFFFFF";
    [ObservableProperty] private string? _message;

    partial void OnShowAllChanged(bool value) => Rebuild();

    partial void OnSelectedChanged(LibraryEntryViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelected));
        OnPropertyChanged(nameof(SelectedName));
        PlaceCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
    }

    public bool HasSelected => Selected is not null;

    public string SelectedName
    {
        get => Selected?.Name ?? "";
        set
        {
            if (Selected is { } s && s.Entry.Name != value) Document.Execute(Edits.RenameLibraryEntry(s.Entry, value));
        }
    }

    /// <summary>The timeline new things go to: the selected one, or the one the selected item is on.</summary>
    public Timeline? Target => _editor.Timelines.TimelineOf(_editor.Timelines.Selected);

    /// <summary>True when entries are narrowed to a timeline.</summary>
    public bool IsFiltered => Target is not null && !ShowAll;

    private void Rebuild()
    {
        var selected = Selected?.Entry;
        var target = Target;
        Entries.Clear();
        Missing.Clear();
        foreach (var entry in Document.Project.Library)
        {
            var problem = target is null ? PlacementProblem.None : Library.CanPlace(entry, target);
            var vm = new LibraryEntryViewModel(entry, problem, Thumbnail);
            if (vm.IsMissing) Missing.Add(vm);
            // Narrowed: only what the timeline can play. "Show all" lists the rest too, greyed with the reason.
            if (ShowAll || target is null || problem == PlacementProblem.None) Entries.Add(vm);
        }
        Selected = Entries.Concat(Missing).FirstOrDefault(e => ReferenceEquals(e.Entry, selected));
        OnPropertyChanged(nameof(IsFiltered));
        OnPropertyChanged(nameof(Target));
        OnPropertyChanged(nameof(IsEmpty));
        PlaceCommand.NotifyCanExecuteChanged();
    }

    public bool IsEmpty => Entries.Count == 0;

    private Bitmap? Thumbnail(LibraryEntry entry)
    {
        if (entry.ResourceId is not { } id || !Library.HasMedia(entry.Kind)) return null;
        if (_thumbnails.TryGetValue(id, out var made)) return made;
        try
        {
            using var stream = Document.OpenMedia(id);
            if (stream is null) return _thumbnails[id] = null;
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            var bytes = copy.ToArray();
            var image = entry.Kind == LibraryKind.Video ? GifFrames.Decode(bytes)[0] : ImageDecoder.Decode(bytes);
            return _thumbnails[id] = PixelBitmap.From(image);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or IndexOutOfRangeException)
        {
            return _thumbnails[id] = null;
        }
    }

    // ---- placing

    private bool CanPlaceSelected => Selected is { CanPlace: true } && Target is not null;

    /// <summary>Puts the selected entry on the target timeline at the playhead, or after the item that is there.</summary>
    [RelayCommand(CanExecute = nameof(CanPlaceSelected))]
    private void Place()
    {
        if (Selected is not { } selected || Target is not { } timeline) return;
        Place(selected.Entry, timeline);
    }

    public TimelineItem? Place(LibraryEntry entry, Timeline timeline)
    {
        Message = null;
        if (Library.CanPlace(entry, timeline) != PlacementProblem.None) return null;
        var item = _editor.Timelines.AddItem(timeline, Library.NewItem(entry, timeline), EditorViewModel.DefaultItemLength);
        if (item is null) Message = Strings.Item_NoRoom;
        return item;
    }

    // ---- removing

    private bool CanRemoveSelected => Selected is not null;

    [RelayCommand(CanExecute = nameof(CanRemoveSelected))]
    private void Remove()
    {
        if (Selected is not { } selected) return;
        Message = null;
        try
        {
            Document.Execute(Edits.RemoveLibraryEntry(Document.Project, selected.Entry));
        }
        catch (InvalidOperationException)
        {
            Message = Strings.Library_InUse;   // an item plays it
        }
    }

    // ---- colours and effects

    /// <summary>Adds the colour typed in the panel to the library (for light timelines).</summary>
    [RelayCommand]
    private void AddColor()
    {
        Message = null;
        try
        {
            var c = ColorText.ParseRgb(NewColor);
            string hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            Document.Execute(Edits.AddLibraryEntry(Document.Project, new LibraryEntry { Name = hex, Kind = LibraryKind.Color, Color = hex }));
        }
        catch (InvalidDataException)
        {
            Message = string.Format(Strings.Inspector_BadValue, NewColor);
        }
    }

    /// <summary>Keeps the selected colour or effect item of a light timeline in the library to use again.</summary>
    [RelayCommand]
    private void SaveSelectedItem()
    {
        Message = null;
        if (_editor.Timelines.Selected is not TimelineItem { Kind: ItemKind.Solid or ItemKind.Effect } item) return;
        var entry = item.Kind == ItemKind.Solid
            ? new LibraryEntry { Name = item.Color ?? "", Kind = LibraryKind.Color, Color = item.Color }
            : new LibraryEntry
            {
                Name = item.Effect ?? "", Kind = LibraryKind.Effect, Color = item.Color, ColorLast = item.ColorLast, Effect = item.Effect, T1 = item.T1, T2 = item.T2,
            };
        try
        {
            Document.Execute(Edits.AddLibraryEntry(Document.Project, entry));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException)
        {
            Message = string.Format(Strings.Inspector_BadValue, entry.Name);
        }
    }

    // ---- importing

    /// <summary>The import panel while a picture is being cut and scaled; null otherwise.</summary>
    [ObservableProperty] private ImportViewModel? _import;

    public bool IsImporting => Import is not null;

    partial void OnImportChanged(ImportViewModel? value) => OnPropertyChanged(nameof(IsImporting));

    /// <summary>
    /// Asks for a file for the selected timeline's kind and brings it into the library: a picture through the import panel
    /// (scaled to the timeline's height), a GIF or a gradient as it is.
    /// </summary>
    [RelayCommand]
    private async Task ImportAsync()
    {
        Message = null;
        if (_picker() is not { } picker) return;
        var timeline = Target;
        string kind = timeline?.Kind switch
        {
            TimelineKind.Video => LibraryKind.Video,
            TimelineKind.EpicPoi => LibraryKind.Gradient,
            _ => LibraryKind.Image,
        };
        if (timeline is { Kind: TimelineKind.Light }) return;

        string? path = kind == LibraryKind.Video ? await picker.PickGifPathAsync() : await picker.PickImagePathAsync();
        if (path is null) return;
        try
        {
            if (kind == LibraryKind.Image) OpenImport(path, timeline);
            else AddFile(path, kind);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Message = string.Format(Strings.Item_AddFailed, Path.GetFileName(path), ex.Message);
        }
    }

    /// <summary>Opens the import panel for a picture; the height comes from the timeline it is for, if any.</summary>
    public void OpenImport(string path, Timeline? timeline)
    {
        var source = ImageDecoder.DecodeFile(path);
        int height = timeline is null ? 0 : Edits.PixelHeight(timeline);
        Import = new ImportViewModel(Document, path, source, height, heightIsFixed: height > 0, entry =>
        {
            Import = null;
            if (entry is not null) Select(entry);
        });
    }

    /// <summary>A GIF or a gradient goes in as the file it is.</summary>
    public LibraryEntry AddFile(string path, string kind)
    {
        int width, height, frames = 0;
        if (kind == LibraryKind.Video)
        {
            var decoded = GifFrames.DecodeFile(path);
            if (decoded.Count == 0) throw new InvalidDataException(string.Format(Strings.Item_BadMedia, Path.GetFileName(path)));
            (width, height, frames) = (decoded[0].Width, decoded[0].Height, decoded.Count);
        }
        else
        {
            var image = ImageDecoder.DecodeFile(path);
            (width, height) = (image.Width, image.Height);
        }
        string id = Document.AddMedia(path);
        var existing = Document.Project.Library.FirstOrDefault(e => e.ResourceId == id && e.Kind == kind);
        if (existing is not null) return existing;
        var entry = new LibraryEntry
        {
            Name = Path.GetFileNameWithoutExtension(path), Kind = kind, ResourceId = id, SourcePath = path, Width = width, Height = height, Frames = frames,
        };
        Document.Execute(Edits.AddLibraryEntry(Document.Project, entry));
        Select(entry);
        return entry;
    }

    private void Select(LibraryEntry entry) =>
        Selected = Entries.Concat(Missing).FirstOrDefault(e => ReferenceEquals(e.Entry, entry));

    // ---- missing files

    /// <summary>Asks for the file of a missing entry; every item that played it plays the new file.</summary>
    [RelayCommand]
    private async Task ReplaceAsync(LibraryEntryViewModel? entry)
    {
        entry ??= Selected;
        Message = null;
        if (entry is null || !Library.HasMedia(entry.Entry.Kind) || _picker() is not { } picker) return;
        string? path = entry.Entry.Kind == LibraryKind.Video ? await picker.PickGifPathAsync() : await picker.PickImagePathAsync();
        if (path is null) return;
        try
        {
            ReplaceFile(entry.Entry, path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Message = string.Format(Strings.Item_AddFailed, Path.GetFileName(path), ex.Message);
        }
        catch (InvalidOperationException)
        {
            Message = string.Format(Strings.Library_ReplaceHeight, Path.GetFileName(path));
        }
    }

    public void ReplaceFile(LibraryEntry entry, string path)
    {
        int width, height, frames = 0;
        if (entry.Kind == LibraryKind.Video)
        {
            var decoded = GifFrames.DecodeFile(path);
            if (decoded.Count == 0) throw new InvalidDataException(string.Format(Strings.Item_BadMedia, Path.GetFileName(path)));
            (width, height, frames) = (decoded[0].Width, decoded[0].Height, decoded.Count);
        }
        else
        {
            var image = ImageDecoder.DecodeFile(path);
            (width, height) = (image.Width, image.Height);
        }
        string id = Document.AddMedia(path);
        Document.Execute(Edits.ReplaceLibraryMedia(Document.Project, entry, id, path, width, height, frames));
    }

    /// <summary>Gives entries to projects that came without a library (called when the editor opens).</summary>
    public void Refresh() => Rebuild();
}
