// Stand-ins for the parts of System.Drawing and WPF imaging that PixelTuner 2's export code
// used (System.Drawing.Common runs only on Windows, WPF only on Windows). Pixels and GIF frames
// come from Studio's decoders, so the comparison checks the export logic, not image decoding.
using System;
using System.Collections.Generic;
using System.IO;
using PixelTuner.Core.Imaging;

namespace System.Drawing
{
    public enum RotateFlipType { RotateNoneFlipY = 6 }

    public class Image
    {
        private string _path;

        public static Image FromFile(string path)
        {
            var image = new Bitmap(ImageDecoder.DecodeFile(path));
            image._path = path;
            return image;
        }

        public Guid[] FrameDimensionsList => [Imaging.FrameDimension.Time.Guid];

        public int GetFrameCount(Imaging.FrameDimension dimension) =>
            _path is null ? 1 : System.Windows.Media.Imaging.GifBitmapDecoder.DecodeFrames(File.ReadAllBytes(_path)).Count;
    }

    public class Bitmap : Image
    {
        private PixelImage _image;

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

        public void SetPixel(int x, int y, Color c) => _image[x, y] = new Rgb(c.R, c.G, c.B);

        public void RotateFlip(RotateFlipType type)
        {
            if (type != RotateFlipType.RotateNoneFlipY) throw new NotSupportedException();
            _image = _image.FlippedVertically();
        }

        /// <summary>PixelTuner 2 saved debug copies of video bitmaps; nothing to save here.</summary>
        public void Save(string path)
        {
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

    public sealed class FrameDimension(Guid guid)
    {
        public static FrameDimension Time { get; } = new(new Guid("6aedbd6d-3fb5-418a-83a6-7f45229dc872"));
        public Guid Guid { get; } = guid;
    }
}

namespace System.Windows.Media.Imaging
{
    public enum BitmapCreateOptions { PreservePixelFormat }
    public enum BitmapCacheOption { Default }

    public sealed class BitmapFrame(PixelImage image)
    {
        internal PixelImage Image { get; } = image;
    }

    public sealed class BmpBitmapEncoder
    {
    }

    /// <summary>WPF's GIF decoder: here every frame as Studio's GifFrames decodes it.</summary>
    public sealed class GifBitmapDecoder
    {
        public GifBitmapDecoder(Stream stream, BitmapCreateOptions options, BitmapCacheOption cache)
        {
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            stream.Dispose();
            var frames = new List<BitmapFrame>();
            foreach (var frame in DecodeFrames(copy.ToArray())) frames.Add(new BitmapFrame(frame));
            Frames = frames;
        }

        public IList<BitmapFrame> Frames { get; }

        /// <summary>
        /// GIF frames; any other picture is one frame. PixelTuner 2's blank video items were
        /// one-frame black GIFs; the tests write them as black BMPs, which decode the same.
        /// </summary>
        internal static IReadOnlyList<PixelImage> DecodeFrames(byte[] data) =>
            data.Length >= 3 && data[0] == 'G' && data[1] == 'I' && data[2] == 'F'
                ? GifFrames.Decode(data)
                : [ImageDecoder.Decode(data)];
    }
}

namespace Common.Imaging
{
    public static partial class ImageHelper
    {
        public static System.Drawing.Bitmap BitmapImageToBitmap<TEncoder>(System.Windows.Media.Imaging.BitmapFrame frame) =>
            new(frame.Image);
    }
}
