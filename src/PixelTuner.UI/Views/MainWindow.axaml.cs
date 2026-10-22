using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using PixelTuner.Package;
using PixelTuner.UI.LedLayouts;
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
        if (e.DataTransfer.Contains(LayoutDrag.Format)) return;   // a layout from the canvas panel: the timelines answer
        e.DragEffects = DroppedPackage(e) is null ? DragDropEffects.None : DragDropEffects.Copy;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DroppedPackage(e) is { } path && DataContext is MainViewModel vm)
            await vm.OpenPathAsync(path);
    }
}
