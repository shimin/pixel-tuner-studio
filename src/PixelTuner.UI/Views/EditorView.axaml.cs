using Avalonia.Controls;
using Avalonia.Input;
using PixelTuner.UI.ViewModels;

namespace PixelTuner.UI.Views;

public partial class EditorView : UserControl
{
    public EditorView() => InitializeComponent();

    // A double click on a library entry puts it on the selected timeline, like the button.
    private void OnEntryDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is EditorViewModel { Gallery.PlaceCommand: { } place } && place.CanExecute(null)) place.Execute(null);
    }
}
