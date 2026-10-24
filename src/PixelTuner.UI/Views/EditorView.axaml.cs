using Avalonia.Controls;
using Avalonia.Input;
using Avalonia;
using Avalonia.Interactivity;
using PixelTuner.UI.LibraryPanel;
using PixelTuner.UI.ViewModels;

namespace PixelTuner.UI.Views;

public partial class EditorView : UserControl
{
    private EditorViewModel? _editor;
    private GridLength _panelHeight = new(320);

    public EditorView()
    {
        InitializeComponent();
        // The list marks the item pressed as handled, so the drag is watched on the way down.
        EntryList.AddHandler(PointerPressedEvent, OnEntryPressed, RoutingStrategies.Tunnel);
        EntryList.AddHandler(PointerMovedEvent, OnEntryMoved, RoutingStrategies.Tunnel);
        EntryList.AddHandler(PointerReleasedEvent, OnEntryReleased, RoutingStrategies.Tunnel);
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

    // ---- an entry of the library dragged onto a timeline (the same entry also goes there by a double click or the button)

    private LibraryEntryViewModel? _pressedEntry;
    private PointerPressedEventArgs? _pressedArgs;
    private Point _pressedAt;
    private bool _dragging;

    /// <summary>True while an entry of the library is being dragged (for tests; the platform drag loop is not there to end it).</summary>
    public bool IsDraggingEntry => _dragging;

    private void OnEntryPressed(object? sender, PointerPressedEventArgs e)
    {
        _pressedEntry = e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ? (e.Source as StyledElement)?.DataContext as LibraryEntryViewModel : null;
        _pressedArgs = e;
        _pressedAt = e.GetPosition(this);
    }

    private void OnEntryReleased(object? sender, PointerReleasedEventArgs e) => _pressedEntry = null;

    private async void OnEntryMoved(object? sender, PointerEventArgs e)
    {
        if (_pressedEntry is not { } entry || _pressedArgs is not { } pressed || _dragging || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var at = e.GetPosition(this);
        if (Math.Abs(at.X - _pressedAt.X) + Math.Abs(at.Y - _pressedAt.Y) <= LibraryDrag.Distance) return;
        _pressedEntry = null;
        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.Create(LibraryDrag.Format, entry.Entry.Id.ToString()));
        _dragging = true;
        try
        {
            await DragDrop.DoDragDropAsync(pressed, transfer, DragDropEffects.Copy);
        }
        finally
        {
            _dragging = false;
        }
    }
}
