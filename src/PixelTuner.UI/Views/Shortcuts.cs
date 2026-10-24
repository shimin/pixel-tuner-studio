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
    public static KeyGesture Undo { get; } = new(Key.Z, Primary);

    // macOS: Cmd+Shift+Z. Windows: Ctrl+Y (Ctrl+Shift+Z works too, as in most editors).
    public static KeyGesture Redo { get; } = OperatingSystem.IsMacOS() ? new(Key.Z, Primary | KeyModifiers.Shift) : new(Key.Y, Primary);
    public static KeyGesture RedoAlternate { get; } = new(Key.Z, Primary | KeyModifiers.Shift);
    public static KeyGesture AddMarker { get; } = new(Key.M, Primary);

    // What the menu shows. The keys themselves (Delete, and Backspace which is the Delete key of a Mac keyboard) are handled by the window
    // when no text box has the focus (see MainWindow.OnKeyDown): a key binding would take them away from the text boxes.
    // On macOS the menu item has Cmd+Backspace, as in Finder, because a menu key equivalent without modifiers is taken before the focused text box.
    public static KeyGesture Delete { get; } = OperatingSystem.IsMacOS() ? new(Key.Back, Primary) : new(Key.Delete);
    public static KeyGesture ZoomIn { get; } = new(Key.OemPlus, Primary);
    public static KeyGesture ZoomOut { get; } = new(Key.OemMinus, Primary);
    public static KeyGesture ZoomFit { get; } = new(Key.D0, Primary);
    public static KeyGesture Close { get; } = new(Key.W, Primary);
}
