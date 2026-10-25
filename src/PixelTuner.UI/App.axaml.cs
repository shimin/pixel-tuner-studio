using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using PixelTuner.Package;
using PixelTuner.UI.DevicesPanel;
using PixelTuner.UI.Services;
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
        // Studio opens projects by path and remembers them; Stage opens a stream, which has no path to come back to.
        var vm = new MainViewModel(Product.Current.CanEdit ? new RecentProjects(RecentProjects.DefaultFile(Product.Current.Name)) : null);
        DataContext = vm;   // the macOS app menu binds to it

        switch (ApplicationLifetime)
        {
            // Windows and macOS: a full window.
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow = new MainWindow { DataContext = vm };
                // The search for devices runs as long as the app does, whatever is open in it: a transfer to a device is not in its way.
                DeviceHub.Current?.StartSearching();
                desktop.Exit += (_, _) =>
                {
                    vm.Dispose();
                    DeviceHub.Current?.StopSearchingAsync().Wait(TimeSpan.FromSeconds(5));
                };
                OpenFilesFromLaunch(vm, desktop.Args);
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

        // macOS opens a .pxpkg that was double-clicked while the app is running through this.
        if (this.TryGetFeature<IActivatableLifetime>() is { } activatable)
            activatable.Activated += (_, e) =>
            {
                if (e is FileActivatedEventArgs { Files.Count: > 0 } files
                    && files.Files[0].TryGetLocalPath() is { } path)
                    _ = vm.OpenPathAsync(path);
            };
    }

    /// <summary>Windows starts the app with the file as an argument when a .pxpkg is double-clicked.</summary>
    private static void OpenFilesFromLaunch(MainViewModel vm, string[]? args)
    {
        string? path = args?.FirstOrDefault(a => a.EndsWith(PxPackageFormat.Extension, StringComparison.OrdinalIgnoreCase) && File.Exists(a));
        if (path is not null) _ = vm.OpenPathAsync(path);
    }
}
