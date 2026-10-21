using Avalonia;
using PixelTuner.UI;

namespace PixelTuner.Desktop;

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
            .LogToTrace();
}
