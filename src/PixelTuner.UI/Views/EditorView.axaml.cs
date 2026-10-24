using Avalonia.Controls;
using Avalonia.Input;
using PixelTuner.UI.ViewModels;

namespace PixelTuner.UI.Views;

public partial class EditorView : UserControl
{
    private EditorViewModel? _editor;
    private GridLength _panelHeight = new(320);

    public EditorView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_editor is not null) _editor.PropertyChanged -= EditorChanged;
            _editor = DataContext as EditorViewModel;
            if (_editor is not null) _editor.PropertyChanged += EditorChanged;
            ApplyBottomPanel();
        };
    }

    private void EditorChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorViewModel.IsBottomPanelVisible)) ApplyBottomPanel();
    }

    // A folded panel has no height; the height the user set is kept for when it comes back.
    private void ApplyBottomPanel()
    {
        var row = Root.RowDefinitions[4];
        bool visible = _editor?.IsBottomPanelVisible ?? true;
        if (visible)
        {
            if (row.Height.Value == 0) row.Height = _panelHeight;
        }
        else if (row.Height.Value != 0)
        {
            _panelHeight = row.Height;
            row.Height = new GridLength(0);
        }
    }

    // A double click on a library entry puts it on the selected timeline, like the button.
    private void OnEntryDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is EditorViewModel { Gallery.PlaceCommand: { } place } && place.CanExecute(null)) place.Execute(null);
    }
}
