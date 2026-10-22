using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelTuner.Core.Editing;
using PixelTuner.Core.Layout;
using PixelTuner.Core.Model;
using PixelTuner.UI.Resources;
using PixelTuner.UI.Services;
using PixelTuner.UI.ViewModels;

namespace PixelTuner.UI.LedLayouts;

/// <summary>One outline of the drawing of the props, with the LED layout that belongs to it (the layout named like its id), if it has one.</summary>
public sealed class CanvasElementViewModel(CanvasShape shape, LedLayout? layout)
{
    public CanvasShape Shape { get; } = shape;
    public LedLayout? Layout { get; } = layout;

    public string Id => Shape.Id;
    public int Columns => Layout?.Columns ?? 0;
    public int Rows => Layout?.Rows ?? 0;

    /// <summary>The LEDs of the prop.</summary>
    public int ActiveLeds => Layout?.Leds.Count ?? 0;

    /// <summary>The prop has LEDs marked on it.</summary>
    public bool HasMarkup => ActiveLeds > 0;

    public string Description => Layout is { Columns: > 0 } ? string.Format(Strings.Canvas_Shape, Id, Columns, Rows, ActiveLeds) : string.Format(Strings.Canvas_ShapeUnmarked, Id);
}

/// <summary>
/// The drawing of the props (PixelTuner 2's canvas pane): an SVG whose outlines are the devices, shown with the markup state of each. A
/// double click opens the LED markup window for an outline, which creates the layout named like it; markup can be copied from one outline to
/// another, and an outline's layout given to the selected video timeline. The SVG is a media file of the project; importing one is an edit.
/// </summary>
public sealed partial class CanvasPanelViewModel : ObservableObject
{
    // The copied markup is shared by every project of the app, as PixelTuner 2 kept it.
    private static (int Columns, int Rows, List<int> Leds, int TilesMargin)? s_copied;

    private readonly EditorViewModel _editor;
    private readonly Func<IFilePicker?> _picker;
    private string? _drawingId;
    private CanvasDrawing? _drawing;

    public CanvasPanelViewModel(EditorViewModel editor, Func<IFilePicker?> picker)
    {
        _editor = editor;
        _picker = picker;
        Refresh();
    }

    /// <summary>Forgets the copied markup (for tests; the app never does).</summary>
    public static void ForgetCopiedMarkup() => s_copied = null;

    private ProjectDocumentAccess Document => new(_editor.Document);

    private readonly record struct ProjectDocumentAccess(Package.ProjectDocument Document)
    {
        public ShowProject Project => Document.Project;
    }

    // ---- what is shown

    public CanvasDrawing? Drawing => _drawing;
    public bool HasDrawing => _drawing is not null;
    public bool HasNoDrawing => _drawing is null;

    public IReadOnlyList<CanvasElementViewModel> Elements { get; private set; } = [];

    [ObservableProperty] private CanvasElementViewModel? _selected;

    [ObservableProperty] private string? _message;

    /// <summary>The outline under the pointer, for the line under the drawing.</summary>
    [ObservableProperty] private string _hoverText = "";

    /// <summary>Raised when the drawing or the markup state of an outline changed: the control draws again.</summary>
    public event EventHandler? Changed;

    partial void OnSelectedChanged(CanvasElementViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelected));
        NotifyCommands();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool HasSelected => Selected is not null;

    /// <summary>Reads the drawing of the project again and matches its outlines with the layouts. Called when the project changed.</summary>
    public void Refresh()
    {
        string? id = Document.Project.CanvasResourceId;
        if (id != _drawingId)
        {
            _drawingId = id;
            _drawing = null;
            if (id is not null)
            {
                using var stream = _editor.Document.OpenMedia(id);
                if (stream is null)
                {
                    Message = Strings.Canvas_Unreadable;
                }
                else
                {
                    using var reader = new StreamReader(stream);
                    _drawing = CanvasDrawing.TryParse(reader.ReadToEnd());
                    Message = _drawing is null ? Strings.Canvas_Unreadable : null;
                }
            }
            OnPropertyChanged(nameof(Drawing));
            OnPropertyChanged(nameof(HasDrawing));
            OnPropertyChanged(nameof(HasNoDrawing));
        }

        string? selectedId = Selected?.Id;
        Elements = _drawing is null ? [] : [.. _drawing.Shapes.Select(shape => new CanvasElementViewModel(shape, LayoutOf(shape.Id)))];
        OnPropertyChanged(nameof(Elements));
        Selected = Elements.FirstOrDefault(e => e.Id == selectedId);
        NotifyCommands();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private LedLayout? LayoutOf(string id) => Document.Project.LedLayouts.FirstOrDefault(l => l.Name == id);

    /// <summary>The outline of the drawing that a layout is marked up on (the one with its name); null for a layout of its own.</summary>
    public CanvasShape? ShapeOf(LedLayout layout) => _drawing?.Find(layout.Name);

    // ---- the pointer

    /// <summary>Selects the outline under a point of the drawing (drawing units); none when the point is on none.</summary>
    public void SelectAt(double x, double y) =>
        Selected = _drawing?.HitTest(x, y) is { } shape ? Elements.First(e => e.Id == shape.Id) : null;

    public void Hover(double x, double y) =>
        HoverText = _drawing?.HitTest(x, y) is { } shape ? Elements.First(e => e.Id == shape.Id).Description : "";

    /// <summary>The page to draw, in drawing units.</summary>
    public (double X, double Y, double Width, double Height) Page => _drawing?.Page ?? default;

    // ---- the drawing of the project

    [RelayCommand]
    private async Task ImportSvg()
    {
        if (_picker() is not { } picker) return;
        string? path = await picker.PickSvgPathAsync();
        if (path is null) return;
        string name = Path.GetFileName(path);
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Message = string.Format(Strings.Canvas_ReadFailed, name, ex.Message);
            return;
        }
        SetDrawing(bytes, name);
    }

    [RelayCommand]
    private void UseSchema(string? schema)
    {
        if (schema is null) return;
        SetDrawing(System.Text.Encoding.UTF8.GetBytes(CanvasSchemas.Read(schema)), schema + ".svg");
    }

    private void SetDrawing(byte[] svg, string name)
    {
        var drawing = CanvasDrawing.TryParse(System.Text.Encoding.UTF8.GetString(svg));
        if (drawing is null)
        {
            Message = string.Format(Strings.Canvas_NotSvg, name);
            return;
        }
        if (drawing.Shapes.Count == 0)
        {
            Message = string.Format(Strings.Canvas_NoShapes, name);
            return;
        }
        // The markup stays with the layouts, named by outline: an outline of the new drawing with the same id takes the old markup.
        string id = _editor.Document.AddMedia(svg, name);
        Message = null;
        _editor.Document.Execute(Edits.SetCanvas(Document.Project, id));
        Refresh();
    }

    [RelayCommand(CanExecute = nameof(HasDrawing))]
    private void RemoveDrawing()
    {
        _editor.Document.Execute(Edits.SetCanvas(Document.Project, null));
        Refresh();
    }

    // ---- the markup of an outline

    [RelayCommand(CanExecute = nameof(HasSelected))]
    private void Edit()
    {
        if (Selected is not { } element) return;
        _editor.OpenMarkup(new LedMarkupViewModel(_editor.Document, element.Layout, null, element.Id, _editor.CloseMarkup, element.Shape));
    }

    /// <summary>A drag of an outline without markup was started: there is no layout to give to a timeline yet.</summary>
    public void RefuseDrag() => Message = Strings.Canvas_DragNeedsMarkup;

    /// <summary>Opens the markup window for an outline (a double click).</summary>
    public void Edit(CanvasElementViewModel element)
    {
        Selected = element;
        Edit();
    }

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private void CopyMarkup()
    {
        if (Selected is not { Layout: { } layout } element) return;
        s_copied = (layout.Columns, layout.Rows, [.. layout.Leds], layout.TilesMargin);
        Message = string.Format(Strings.Canvas_Copied, element.Id);
        NotifyCommands();
    }

    private bool CanCopy() => Selected is { Layout.Columns: > 0 };

    /// <summary>Gives the selected outline a copy of the copied markup: the grid, the LEDs and the spacing; its own was replaced.</summary>
    [RelayCommand(CanExecute = nameof(CanPaste))]
    private void PasteMarkup()
    {
        if (Selected is not { } element || s_copied is not { } copied) return;
        Message = null;
        if (element.Layout is { } layout)
        {
            var markup = LedMarkup.Load(copied.Columns, copied.Rows, copied.Leds);
            _editor.Document.Execute(Edits.SetLedMarkup(layout, markup, copied.TilesMargin));
        }
        else
        {
            var created = new LedLayout { Name = element.Id, Columns = copied.Columns, Rows = copied.Rows, Leds = [.. copied.Leds], TilesMargin = copied.TilesMargin };
            _editor.Document.Execute(Edits.AddLedLayout(Document.Project, created));
        }
        Refresh();
    }

    private bool CanPaste() => Selected is not null && s_copied is not null;

    /// <summary>The layout of the selected outline goes to the selected video timeline (PixelTuner 2 did this by dragging the outline onto it).</summary>
    [RelayCommand(CanExecute = nameof(CanUseForTimeline))]
    private void UseForTimeline()
    {
        if (Selected is not { Layout: { } layout } || _editor.SelectedVideoTimeline is not { } timeline)
        {
            Message = Strings.Canvas_NoVideoTimeline;
            return;
        }
        Message = null;
        _editor.Document.Execute(Edits.SetLedLayout(Document.Project, timeline, layout.Id));
    }

    private bool CanUseForTimeline() => Selected is { Layout.Columns: > 0 };

    private void NotifyCommands()
    {
        RemoveDrawingCommand.NotifyCanExecuteChanged();
        EditCommand.NotifyCanExecuteChanged();
        CopyMarkupCommand.NotifyCanExecuteChanged();
        PasteMarkupCommand.NotifyCanExecuteChanged();
        UseForTimelineCommand.NotifyCanExecuteChanged();
    }
}
