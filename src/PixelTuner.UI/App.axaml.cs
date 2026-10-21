using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PixelTuner.UI.ViewModels;
using PixelTuner.UI.Views;

namespace PixelTuner.UI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        // The app menu title on macOS.
        Name = Product.Current.Name;
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var vm = new MainViewModel();

        switch (ApplicationLifetime)
        {
            // Windows and macOS: a full window.
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow = new MainWindow { DataContext = vm };
                break;
            // Android: the activity hosts a single view.
            case IActivityApplicationLifetime activity:
                activity.MainViewFactory = () => new MainView { DataContext = vm };
                break;
            // iOS: a single view.
            case ISingleViewApplicationLifetime single:
                single.MainView = new MainView { DataContext = vm };
                break;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
