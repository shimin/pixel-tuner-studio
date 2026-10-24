using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using PixelTuner.Package;
using PixelTuner.UI.LedLayouts;
using PixelTuner.UI.LibraryPanel;
using PixelTuner.UI.ViewModels;

namespace PixelTuner.UI.Views;

public partial class MainWindow : Window
{
    private bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    /// <summary>
    /// Delete and Backspace remove what is selected on the timelines, unless a text box has the focus (they edit its text then) or the
    /// LED markup window is open (it has its own).
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || e.Key is not (Key.Delete or Key.Back)) return;
        if (DataContext is not MainViewModel { Editor: { IsEditingMarkup: false } editor }) return;
        if (FocusManager?.GetFocusedElement() is TextBox) return;
        if (!editor.DeleteSelectedCommand.CanExecute(null)) return;
        editor.DeleteSelectedCommand.Execute(null);
        e.Handled = true;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainViewModel vm) vm.ExitRequested += (_, _) => Close();
    }

    /// <summary>A window with unsaved changes asks before it closes.</summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_closeConfirmed || DataContext is not MainViewModel vm) return;
        e.Cancel = true;
        _ = ConfirmThenCloseAsync(vm);
    }

    private async Task ConfirmThenCloseAsync(MainViewModel vm)
    {
        if (!await vm.ConfirmDiscardAsync()) return;
        _closeConfirmed = true;
        Close();
    }

    private static string? DroppedPackage(DragEventArgs e) =>
        e.DataTransfer.TryGetFiles()?
            .Select(f => f.TryGetLocalPath())
            .FirstOrDefault(p => p is not null && p.EndsWith(PxPackageFormat.Extension, StringComparison.OrdinalIgnoreCase));

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Contains(LayoutDrag.Format) || e.DataTransfer.Contains(LibraryDrag.Format)) return;   // a layout or a library entry: the timelines answer
        e.DragEffects = DroppedPackage(e) is null ? DragDropEffects.None : DragDropEffects.Copy;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DroppedPackage(e) is { } path && DataContext is MainViewModel vm)
            await vm.OpenPathAsync(path);
    }
}
