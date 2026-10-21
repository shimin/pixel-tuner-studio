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
    public static Product Studio { get; } = new("Pixel Tuner Studio", canEdit: true);
    public static Product Stage { get; } = new("Pixel Tuner Stage", canEdit: false);

    public static Product Current { get; set; } = Studio;

    private Product(string name, bool canEdit)
    {
        Name = name;
        CanEdit = canEdit;
    }

    /// <summary>
    /// Studio creates, edits, saves and imports projects; Stage only opens them. Importing a
    /// PixelTuner 2 project needs its media folders next to the file, so it is a desktop job.
    /// </summary>
    public bool CanEdit { get; }

    /// <summary>Major.minor.patch of the running app.</summary>
    public static string Version =>
        System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}" : "";

    public string Name { get; }

    public override string ToString() => Name;
}
