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
    public static Product Studio { get; } = new("Pixel Tuner Studio", canImportLegacy: true);
    public static Product Stage { get; } = new("Pixel Tuner Stage", canImportLegacy: false);

    public static Product Current { get; set; } = Studio;

    private Product(string name, bool canImportLegacy)
    {
        Name = name;
        CanImportLegacy = canImportLegacy;
    }

    /// <summary>
    /// Importing a PixelTuner 2 project needs its media folders next to the file, so only the
    /// desktop app offers it; phones open the resulting .pxpkg.
    /// </summary>
    public bool CanImportLegacy { get; }

    public string Name { get; }

    public override string ToString() => Name;
}
