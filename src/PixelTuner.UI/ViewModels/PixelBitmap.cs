using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PixelTuner.Core.Imaging;

namespace PixelTuner.UI.ViewModels;

/// <summary>Turns decoded device pixels into something Avalonia can draw.</summary>
internal static class PixelBitmap
{
    public static Bitmap? From(PixelImage image)
    {
        if (image.Width == 0 || image.Height == 0) return null;
        var bitmap = new WriteableBitmap(new PixelSize(image.Width, image.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Opaque);
        using var frame = bitmap.Lock();
        var row = new byte[image.Width * 4];
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var c = image[x, y];
                row[x * 4] = c.R;
                row[x * 4 + 1] = c.G;
                row[x * 4 + 2] = c.B;
                row[x * 4 + 3] = 255;
            }
            System.Runtime.InteropServices.Marshal.Copy(row, 0, frame.Address + y * frame.RowBytes, row.Length);
        }
        return bitmap;
    }
}
