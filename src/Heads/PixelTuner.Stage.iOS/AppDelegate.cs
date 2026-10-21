using Avalonia;
using Avalonia.iOS;
using Foundation;
using PixelTuner.UI;

namespace PixelTuner.Stage.iOS;

[Register("AppDelegate")]
#pragma warning disable CA1711 // UIKit requires the "Delegate" suffix.
public partial class AppDelegate : AvaloniaAppDelegate<App>
#pragma warning restore CA1711
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        Product.Current = Product.Stage;
        return base.CustomizeAppBuilder(builder).WithInterFont();
    }
}
