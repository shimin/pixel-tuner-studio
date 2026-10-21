using Android.App;
using Android.Content;
using Android.Content.PM;
using Avalonia.Android;

namespace PixelTuner.Stage.Android;

[Activity(
    Label = "Pixel Tuner Stage",
    Theme = "@style/PtTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
// Opens .pxpkg files shared from Telegram, mail, the file manager and so on.
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault },
    DataMimeType = "application/vnd.pixeltuner.package+zip")]
public class MainActivity : AvaloniaMainActivity;
