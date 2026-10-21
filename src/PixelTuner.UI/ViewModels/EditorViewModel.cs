using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelTuner.Core.Editing;
using PixelTuner.Core.Model;
using PixelTuner.Package;
using PixelTuner.UI.Resources;
using PixelTuner.UI.Timelines;

namespace PixelTuner.UI.ViewModels;

/// <summary>The editor of one open project: the timelines, their commands and the inspector.</summary>
public sealed partial class EditorViewModel : ObservableObject
{
    // Saving reports from a pool thread; bindings may only be touched on the UI thread.
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;

    public EditorViewModel(ProjectDocument document)
    {
        Document = document;
        Timelines = new TimelineEditor(document);
        Timelines.Changed += (_, _) => OnUi(Refresh);
        Timelines.SelectionChanged += (_, _) => OnUi(Refresh);
    }

    public ProjectDocument Document { get; }

    public TimelineEditor Timelines { get; }

    private void OnUi(Action action)
    {
        if (_ui is null || SynchronizationContext.Current == _ui) action();
        else _ui.Post(_ => action(), null);
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(InspectorTitle));
        OnPropertyChanged(nameof(CanRename));
        OnPropertyChanged(nameof(SelectionName));
        OnPropertyChanged(nameof(StartText));
        OnPropertyChanged(nameof(LengthText));
        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(IsItemSelected));
        OnPropertyChanged(nameof(IsMarkerSelected));
        DeleteSelectedCommand.NotifyCanExecuteChanged();
    }

    // ---- commands

    [RelayCommand] void AddPixelTimeline() => Add(TimelineKind.Pixel, "#FF2D7DD2");
    [RelayCommand] void AddVideoTimeline() => Add(TimelineKind.Video, "#FF2FA84F");
    [RelayCommand] void AddEpicTimeline() => Add(TimelineKind.EpicPoi, "#FFE07A1F");
    [RelayCommand] void AddLightTimeline() => Add(TimelineKind.Light, "#FFC9A227");

    private void Add(string kind, string color) =>
        Timelines.AddTimeline(string.Format(Strings.Timeline_DefaultName, Timelines.Project.Timelines.Count + 1), kind, color);

    [RelayCommand]
    void AddMarker() =>
        Timelines.AddMarkerAtPlayhead(string.Format(Strings.Marker_DefaultName, Timelines.Project.Markers.Count + 1));

    [RelayCommand(CanExecute = nameof(CanDelete))]
    void DeleteSelected() => Timelines.DeleteSelected();

    private bool CanDelete => Timelines.CanDelete;

    // The view is the centre of the time area when a command zooms.
    private double Centre => TimelineEditor.HeaderWidth + Timelines.TimeAreaWidth / 2;

    [RelayCommand] void ZoomIn() => Timelines.ZoomAt(1.5, Centre);
    [RelayCommand] void ZoomOut() => Timelines.ZoomAt(1 / 1.5, Centre);
    [RelayCommand] void ZoomFit() => Timelines.ZoomToFit();

    // ---- inspector

    public bool HasSelection => Timelines.Selected is not null;
    public bool IsItemSelected => Timelines.Selected is TimelineItem;
    public bool IsMarkerSelected => Timelines.Selected is Marker;
    public bool CanRename => Timelines.Selected is Timeline or Marker;

    public string InspectorTitle => Timelines.Selected switch
    {
        Timeline => Strings.Inspector_Timeline,
        TimelineItem => Strings.Inspector_Item,
        Marker => Strings.Inspector_Marker,
        _ => "",
    };

    public string SelectionName
    {
        get => Timelines.Selected switch { Timeline t => t.Name, Marker m => m.Name, _ => "" };
        set
        {
            switch (Timelines.Selected)
            {
                case Timeline t when t.Name != value:
                    Document.Execute(Edits.RenameTimeline(t, value));
                    break;
                case Marker m when m.Name != value:
                    Document.Execute(Edits.RenameMarker(m, value));
                    break;
            }
        }
    }

    public string StartText => Timelines.Selected is TimelineItem i ? TimeFormat.Precise(i.Start) : "";
    public string LengthText => Timelines.Selected is TimelineItem i ? TimeFormat.Precise(i.Duration) : "";
    public string PositionText => Timelines.Selected is Marker m ? TimeFormat.Precise(m.Position) : "";
}
