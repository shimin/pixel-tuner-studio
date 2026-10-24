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

    /// <summary>LightMaster colours and effects are a fixed list: they are not in the project, so they cannot be renamed or removed.</summary>
    public bool IsFixed => !Library.HasMedia(Entry.Kind);

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

/// <summary>One part of the library: the things one kind of timeline plays (PixelTuner 2 had a pane for each).</summary>
/// <param name="Kind">The <see cref="LibraryKind"/> of its entries; <see cref="LibraryKind.Effect"/> for the LightMaster's fixed list.</param>
/// <param name="TimelineKind">The kind of timeline that plays them.</param>
public sealed record LibrarySection(string Kind, string TimelineKind, string Title)
{
    /// <summary>Files come in by import; the LightMaster's colours and effects are given and cannot be added or removed.</summary>
    public bool IsFixed => !Library.HasMedia(Kind);
}

/// <summary>
/// The library panel of the editor: what the project has to put on timelines, in a section for each kind of timeline. Things are
/// imported into their section and from there put on a timeline; a thing the selected timeline cannot play is shown dimmed with the
/// reason, so what goes on a timeline always builds into the .bin.
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

    public static IReadOnlyList<LibrarySection> Sections { get; } =
    [
        new(LibraryKind.Image, TimelineKind.Pixel, Strings.Library_SectionPixel),
        new(LibraryKind.Video, TimelineKind.Video, Strings.Library_SectionVideo),
        new(LibraryKind.Gradient, TimelineKind.EpicPoi, Strings.Library_SectionEpic),
        new(LibraryKind.Effect, TimelineKind.Light, Strings.Library_SectionLight),
    ];

    /// <summary>
    /// The LightMaster's colour and effects as PixelTuner 2's effects pane had them (solid colour, linear transition, sine, square and
    /// saw-tooth wave) with the defaults a new light item gets. Fixed: not in the project.
    /// </summary>
    public static IReadOnlyList<LibraryEntry> LightEntries { get; } =
    [
        new() { Name = Strings.Effect_Solid, Kind = LibraryKind.Color, Color = "#FFFFFF" },
        Effect(Strings.Effect_LinearTransition, LightEffectKind.LinearTransition),
        Effect(Strings.Effect_SineWave, LightEffectKind.SineWave),
        Effect(Strings.Effect_SquareWave, LightEffectKind.SquareWave),
        Effect(Strings.Effect_SawToothWave, LightEffectKind.SawToothWave),
    ];

    private static LibraryEntry Effect(string name, string effect) =>
        new() { Name = name, Kind = LibraryKind.Effect, Color = "#000000", ColorLast = "#FFFFFF", Effect = effect, T1 = 1000, T2 = 1000 };

    private int _sectionIndex;

    /// <summary>The section shown. It follows the selected timeline when that is of another kind, and can be changed by hand after.</summary>
    public int SectionIndex
    {
        get => _sectionIndex;
        set
        {
            if (value < 0 || value >= Sections.Count || value == _sectionIndex) return;
            _sectionIndex = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Section));
            OnPropertyChanged(nameof(IsFixedSection));
            OnPropertyChanged(nameof(HasHeightFilter));
            Selected = null;
            Rebuild();
        }
    }

    public LibrarySection Section => Sections[_sectionIndex];

    public bool IsFixedSection => Section.IsFixed;

    // ---- height filter (pixel pictures)

    private int _heightFilter;   // 0: every height
    private int[] _heights = [];
    private string[]? _heightItems;
    private bool _notifying;

    /// <summary>The pixel section can be narrowed to the pictures of one height (the number of LEDs).</summary>
    public bool HasHeightFilter => Section.Kind == LibraryKind.Image;

    /// <summary>"All", then each height there is a picture of. The same list until the heights change (see <see cref="EditorViewModel.LayoutNames"/>).</summary>
    public IReadOnlyList<string> HeightFilterItems
    {
        get
        {
            string[] items = [Strings.Library_AllHeights, .. _heights.Select(h => h.ToString())];
            return _heightItems is not null && _heightItems.AsSpan().SequenceEqual(items) ? _heightItems : _heightItems = items;
        }
    }

    public int HeightFilterIndex
    {
        get => _heightFilter == 0 ? 0 : Math.Max(0, Array.IndexOf(_heights, _heightFilter) + 1);
        set
        {
            if (_notifying || value < 0 || value > _heights.Length) return;
            int height = value == 0 ? 0 : _heights[value - 1];
            if (height == _heightFilter) return;
            _heightFilter = height;
            Rebuild();
        }
    }

    /// <summary>What the panel lists now.</summary>
    public ObservableCollection<LibraryEntryViewModel> Entries { get; } = [];

    /// <summary>Entries whose file is missing: the panel lists them apart, with a button to find the file.</summary>
    public ObservableCollection<LibraryEntryViewModel> Missing { get; } = [];

    [ObservableProperty] private LibraryEntryViewModel? _selected;
    [ObservableProperty] private string? _message;

    partial void OnSelectedChanged(LibraryEntryViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelected));
        OnPropertyChanged(nameof(CanRename));
        OnPropertyChanged(nameof(SelectedName));
        PlaceCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
    }

    public bool HasSelected => Selected is not null;

    /// <summary>The name box is for things of the project; the LightMaster's list keeps its names.</summary>
    public bool CanRename => Selected is { IsFixed: false };

    public string SelectedName
    {
        get => Selected?.Name ?? "";
        set
        {
            if (Selected is { IsFixed: false } s && s.Entry.Name != value) Document.Execute(Edits.RenameLibraryEntry(s.Entry, value));
        }
    }

    /// <summary>The timeline new things go to: the selected one, or the one the selected item is on.</summary>
    public Timeline? Target => _editor.Timelines.TimelineOf(_editor.Timelines.Selected);

    private Timeline? _lastTarget;

    // Choosing another timeline brings its section forward; a section picked by hand stays while the same timeline is worked on.
    private void FollowTarget()
    {
        var target = Target;
        if (ReferenceEquals(target, _lastTarget)) return;
        _lastTarget = target;
        if (target is null) return;
        int index = Sections.ToList().FindIndex(x => x.TimelineKind == target.Kind);
        if (index >= 0) SectionIndex = index;
    }

    private void Rebuild()
    {
        FollowTarget();
        var selected = Selected?.Entry;
        var target = Target;
        Entries.Clear();
        Missing.Clear();
        _heights = [.. Document.Project.Library.Where(e => e.Kind == LibraryKind.Image && e.Height > 0).Select(e => e.Height).Distinct().Order()];
        if (_heightFilter != 0 && Array.IndexOf(_heights, _heightFilter) < 0) _heightFilter = 0;
        var shown = Section.IsFixed ? LightEntries : Document.Project.Library.Where(e => e.Kind == Section.Kind);
        if (Section.Kind == LibraryKind.Image && _heightFilter != 0) shown = shown.Where(e => e.Height == _heightFilter);
        foreach (var entry in shown)
        {
            var problem = target is null ? PlacementProblem.None : Library.CanPlace(entry, target);
            var vm = new LibraryEntryViewModel(entry, problem, Thumbnail);
            if (vm.IsMissing) Missing.Add(vm);
            Entries.Add(vm);
        }
        Selected = Entries.FirstOrDefault(e => ReferenceEquals(e.Entry, selected));
        _notifying = true;
        try
        {
            OnPropertyChanged(nameof(HeightFilterItems));
            OnPropertyChanged(nameof(HeightFilterIndex));
        }
        finally { _notifying = false; }
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

    private bool CanRemoveSelected => Selected is { IsFixed: false };

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

    // ---- importing

    /// <summary>The import panel while a picture is being cut and scaled; null otherwise.</summary>
    [ObservableProperty] private ImportViewModel? _import;

    public bool IsImporting => Import is not null;

    partial void OnImportChanged(ImportViewModel? value) => OnPropertyChanged(nameof(IsImporting));

    /// <summary>
    /// Asks for a file and brings it into the section it belongs to by what it is, whichever section is shown: a GIF is a video; a
    /// picture is a pixel picture (through the import panel, scaled to the height of the selected pixel timeline, if there is one), or a
    /// gradient when an E-pic timeline is selected. The section is shown and the new entry selected.
    /// </summary>
    [RelayCommand]
    private async Task ImportAsync()
    {
        Message = null;
        if (_picker() is not { } picker) return;
        string? path = await picker.PickImagePathAsync();
        if (path is null) return;
        string kind = KindOf(path);
        try
        {
            ShowSection(kind);
            if (kind == LibraryKind.Image) OpenImport(path, Target is { Kind: TimelineKind.Pixel } pixel ? pixel : null);
            else AddFile(path, kind);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Message = string.Format(Strings.Item_AddFailed, Path.GetFileName(path), ex.Message);
        }
    }

    private string KindOf(string path) =>
        path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ? LibraryKind.Video
        : Target is { Kind: TimelineKind.EpicPoi } ? LibraryKind.Gradient
        : LibraryKind.Image;

    private void ShowSection(string kind)
    {
        int index = Sections.ToList().FindIndex(x => x.Kind == kind);
        if (index >= 0) SectionIndex = index;
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
        if (existing is not null)
        {
            Select(existing);
            return existing;
        }
        var entry = new LibraryEntry
        {
            Name = Path.GetFileNameWithoutExtension(path), Kind = kind, ResourceId = id, SourcePath = path, Width = width, Height = height, Frames = frames,
        };
        Document.Execute(Edits.AddLibraryEntry(Document.Project, entry));
        Select(entry);
        return entry;
    }

    private void Select(LibraryEntry entry)
    {
        ShowSection(entry.Kind);
        // A picture the height filter hides would not show: the filter gives way.
        if (entry.Kind == LibraryKind.Image && _heightFilter != 0 && entry.Height != _heightFilter)
        {
            _heightFilter = 0;
            Rebuild();
        }
        Selected = Entries.FirstOrDefault(e => ReferenceEquals(e.Entry, entry));
    }

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
