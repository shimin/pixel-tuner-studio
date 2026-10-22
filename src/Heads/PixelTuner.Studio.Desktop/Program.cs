using Avalonia;
using PixelTuner.UI;
using PixelTuner.UI.DevicesPanel;
using PixelTuner.UI.Services;

namespace PixelTuner.Studio.Desktop;

static class Program
{
    // Nothing that touches Avalonia may run before AppMain: the platform is not initialised yet.
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Also used by the XAML previewer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .AfterSetup(_ =>
            {
                AudioEngine.Current = VlcAudioEngine.TryCreate();
                DeviceHub.Current = new DeviceManagerHub();
            });
}
