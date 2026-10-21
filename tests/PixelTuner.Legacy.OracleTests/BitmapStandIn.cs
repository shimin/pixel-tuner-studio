// Stand-in for the part of System.Drawing.Bitmap that PixelTuner 2's gradient export used
// (System.Drawing.Common runs only on Windows). Pixels come from Studio's decoder, so the
// comparison checks the export logic, not image decoding.
using System;
using PixelTuner.Core.Imaging;

namespace System.Drawing
{
    public class Image
    {
        public static Image FromFile(string path) => new Bitmap(ImageDecoder.DecodeFile(path));
    }

    public class Bitmap : Image
    {
        private readonly PixelImage _image;

        public Bitmap(int width, int height) => _image = PixelImage.Black(width, height);

        internal Bitmap(PixelImage image) => _image = image;

        public int Width => _image.Width;
        public int Height => _image.Height;
        public Imaging.PixelFormat PixelFormat => default;

        public Color GetPixel(int x, int y)
        {
            var c = _image[x, y];
            return Color.FromArgb(c.R, c.G, c.B);
        }

        public Imaging.BitmapData LockBits(Rectangle rect, Imaging.ImageLockMode mode, Imaging.PixelFormat format) =>
            throw new NotSupportedException();

        public void UnlockBits(Imaging.BitmapData data) => throw new NotSupportedException();
    }
}

namespace System.Drawing.Imaging
{
    public enum PixelFormat { Undefined }
    public enum ImageLockMode { ReadOnly }

    public class BitmapData
    {
        public IntPtr Scan0 { get; set; }
        public int Stride { get; set; }
    }
}
