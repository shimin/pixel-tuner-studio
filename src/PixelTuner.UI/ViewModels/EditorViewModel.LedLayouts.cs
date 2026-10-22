using CommunityToolkit.Mvvm.Input;
using PixelTuner.Core.Editing;
using PixelTuner.Core.Model;
using PixelTuner.UI.LedLayouts;
using PixelTuner.UI.Resources;

namespace PixelTuner.UI.ViewModels;

public sealed partial class EditorViewModel
{
    /// <summary>The LED markup window while it is open; it takes the place of the editor.</summary>
    public LedMarkupViewModel? MarkupEditor { get; private set; }

    public bool IsEditingMarkup => MarkupEditor is not null;

    private LedLayout? SelectedLayout =>
        SelectedTimeline is { Kind: TimelineKind.Video, LedLayoutId: { } id } ? Document.Project.LedLayouts.FirstOrDefault(l => l.Id == id) : null;

    public bool CanEditLayout => SelectedLayout is not null;

    private string LayoutTitle(LedLayout layout) =>
        string.IsNullOrEmpty(layout.Name) ? string.Format(Strings.Inspector_LayoutN, Document.Project.LedLayouts.IndexOf(layout) + 1) : layout.Name;

    [RelayCommand]
    private void NewLayout()
    {
        if (SelectedTimeline is not { Kind: TimelineKind.Video } timeline) return;
        Open(new LedMarkupViewModel(Document, null, timeline, string.Format(Strings.Inspector_LayoutN, Document.Project.LedLayouts.Count + 1), Close));
    }

    [RelayCommand]
    private void EditLayout()
    {
        if (SelectedLayout is { } layout) Open(new LedMarkupViewModel(Document, layout, null, LayoutTitle(layout), Close));
    }

    /// <summary>Takes the layout away from the project; the timelines that used it are left without one. Undo brings it back.</summary>
    [RelayCommand]
    private void DeleteLayout()
    {
        if (SelectedLayout is { } layout) Document.Execute(Edits.RemoveLedLayout(Document.Project, layout));
    }

    private void Open(LedMarkupViewModel editor)
    {
        MarkupEditor = editor;
        OnPropertyChanged(nameof(MarkupEditor));
        OnPropertyChanged(nameof(IsEditingMarkup));
    }

    private void Close(bool applied)
    {
        MarkupEditor = null;
        OnPropertyChanged(nameof(MarkupEditor));
        OnPropertyChanged(nameof(IsEditingMarkup));
        Refresh();
    }
}
