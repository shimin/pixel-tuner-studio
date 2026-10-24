using Avalonia.Input;

namespace PixelTuner.UI.LibraryPanel;

/// <summary>What is carried when an entry of the library is dragged onto a timeline: the id of the entry (the LightMaster's fixed entries have ids too).</summary>
public static class LibraryDrag
{
    public static readonly DataFormat<string> Format = DataFormat.CreateStringApplicationFormat("com.pixeltuner.library-entry");

    /// <summary>The pointer must travel this far with the button down before it is a drag and not a click.</summary>
    public const double Distance = 8;
}
