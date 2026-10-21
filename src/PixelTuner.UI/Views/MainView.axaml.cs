using Avalonia;
using Avalonia.Controls;
using PixelTuner.UI.Services;
using PixelTuner.UI.ViewModels;

namespace PixelTuner.UI.Views;

public partial class MainView : UserControl
{
    public MainView() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is MainViewModel vm && TopLevel.GetTopLevel(this) is { } top)
        {
            vm.FilePicker = new StorageFilePicker(top);
            if (top is Window window) vm.Dialogs = new WindowDialogService(window);
        }
    }
}
