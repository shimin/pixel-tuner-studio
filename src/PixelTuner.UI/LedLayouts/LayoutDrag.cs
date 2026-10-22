using Avalonia.Input;

namespace PixelTuner.UI.LedLayouts;

/// <summary>What is carried when the LED layout of an outline is dragged from the canvas panel onto a video timeline: the id of the layout.</summary>
public static class LayoutDrag
{
    public static readonly DataFormat<string> Format = DataFormat.CreateStringApplicationFormat("com.pixeltuner.led-layout");
}
