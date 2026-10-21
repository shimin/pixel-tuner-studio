using PixelTuner.Core.Imaging;

namespace PixelTuner.UI.LibraryPanel;

public enum CropHandle { None, Move, TopLeft, TopRight, BottomLeft, BottomRight, New }

/// <summary>
/// What dragging on the crop preview does, in picture pixels: no drawing in here, so it is tested without a window.
/// Grabbing a corner resizes the rectangle, grabbing inside moves it, and pressing outside starts a new one.
/// </summary>
public static class CropGeometry
{
    /// <param name="x">Pointer in picture pixels.</param>
    /// <param name="grab">How close to a corner counts as on it, in picture pixels (the control converts from screen pixels).</param>
    public static CropHandle HitTest(PixelRect rect, double x, double y, double grab)
    {
        double left = rect.X, top = rect.Y, right = rect.X + rect.Width, bottom = rect.Y + rect.Height;
        bool nearLeft = Math.Abs(x - left) <= grab, nearRight = Math.Abs(x - right) <= grab;
        bool nearTop = Math.Abs(y - top) <= grab, nearBottom = Math.Abs(y - bottom) <= grab;
        if (nearLeft && nearTop) return CropHandle.TopLeft;
        if (nearRight && nearTop) return CropHandle.TopRight;
        if (nearLeft && nearBottom) return CropHandle.BottomLeft;
        if (nearRight && nearBottom) return CropHandle.BottomRight;
        return x >= left && x <= right && y >= top && y <= bottom ? CropHandle.Move : CropHandle.New;
    }

    /// <summary>The rectangle after a drag from (<paramref name="startX"/>, <paramref name="startY"/>) to (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public static PixelRect Drag(CropHandle handle, PixelRect start, double startX, double startY, double x, double y, int imageWidth, int imageHeight)
    {
        double dx = x - startX, dy = y - startY;
        switch (handle)
        {
            case CropHandle.Move:
                return new PixelRect(
                    Math.Clamp((int)Math.Round(start.X + dx), 0, imageWidth - start.Width),
                    Math.Clamp((int)Math.Round(start.Y + dy), 0, imageHeight - start.Height),
                    start.Width, start.Height);
            case CropHandle.New:
                return Between(startX, startY, x, y, imageWidth, imageHeight);
            case CropHandle.TopLeft:
                return Between(x, y, start.X + start.Width, start.Y + start.Height, imageWidth, imageHeight);
            case CropHandle.TopRight:
                return Between(start.X, y, x, start.Y + start.Height, imageWidth, imageHeight);
            case CropHandle.BottomLeft:
                return Between(x, start.Y, start.X + start.Width, y, imageWidth, imageHeight);
            case CropHandle.BottomRight:
                return Between(start.X, start.Y, x, y, imageWidth, imageHeight);
            default:
                return start;
        }
    }

    /// <summary>The rectangle between two corners, whichever way round they are, inside the picture and at least a pixel big.</summary>
    private static PixelRect Between(double x1, double y1, double x2, double y2, int imageWidth, int imageHeight)
    {
        int left = Math.Clamp((int)Math.Round(Math.Min(x1, x2)), 0, imageWidth - 1);
        int top = Math.Clamp((int)Math.Round(Math.Min(y1, y2)), 0, imageHeight - 1);
        int right = Math.Clamp((int)Math.Round(Math.Max(x1, x2)), left + 1, imageWidth);
        int bottom = Math.Clamp((int)Math.Round(Math.Max(y1, y2)), top + 1, imageHeight);
        return new PixelRect(left, top, right - left, bottom - top);
    }
}
