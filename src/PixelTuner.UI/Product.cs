namespace PixelTuner.UI;

/// <summary>
/// The app the shared UI runs in. Studio (Windows, macOS) edits shows; Stage (iPhone, iPad,
/// Android) plays them on stage. Each Stage head sets <see cref="Current"/> before the UI starts.
/// Product names are not translated, so they live here and not in the string resources.
/// Use the app name only where the text is about this app (title, menus); messages from
/// shared code say "Pixel Tuner".
/// </summary>
public sealed class Product
{
    public static Product Studio { get; } = new("Pixel Tuner Studio");
    public static Product Stage { get; } = new("Pixel Tuner Stage");

    public static Product Current { get; set; } = Studio;

    private Product(string name) => Name = name;

    public string Name { get; }

    public override string ToString() => Name;
}
