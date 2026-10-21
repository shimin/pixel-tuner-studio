using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using PixelTuner.UI;

namespace PixelTuner.Stage.Android;

[Application]
public class Application(nint javaReference, JniHandleOwnership transfer)
    : AvaloniaAndroidApplication<App>(javaReference, transfer)
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        Product.Current = Product.Stage;
        return base.CustomizeAppBuilder(builder).WithInterFont();
    }
}
