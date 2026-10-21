using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using PixelTuner.UI;

namespace PixelTuner.Android;

[Application]
public class Application(nint javaReference, JniHandleOwnership transfer)
    : AvaloniaAndroidApplication<App>(javaReference, transfer)
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).WithInterFont();
}
