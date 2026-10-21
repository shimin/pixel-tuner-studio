using Avalonia.Input;

namespace PixelTuner.UI.Views;

/// <summary>
/// Keyboard shortcuts of the desktop menus: Ctrl on Windows, Cmd on macOS.
/// </summary>
public static class Shortcuts
{
    private static readonly KeyModifiers Primary = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

    public static KeyGesture New { get; } = new(Key.N, Primary);
    public static KeyGesture Open { get; } = new(Key.O, Primary);
    public static KeyGesture Import { get; } = new(Key.I, Primary | KeyModifiers.Shift);
    public static KeyGesture Save { get; } = new(Key.S, Primary);
    public static KeyGesture SaveAs { get; } = new(Key.S, Primary | KeyModifiers.Shift);
    public static KeyGesture Close { get; } = new(Key.W, Primary);
}
