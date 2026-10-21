using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace Communication.Pixelate
{
    public class ImagesCollectionVideoItem : ImagesCollectionItem
    {
        private List<Point> _points;
        private int _xOffset = 0;
        private int _yOffset = 0;

        public ImagesCollectionVideoItem(string imagePath, List<Point> points,  ColorDepth depth, int xOffset, int yOffset, byte brightness)
            : base(imagePath, (UInt16)points.Count, depth, brightness, false)
        {
            _xOffset = xOffset;
            _yOffset = yOffset;
            _points = points;
            Load((ushort) points.Count);
        }

        private void Load(ushort targetHeight)
        {
            if (File.Exists(_path))
            {
                Image gifImg = Image.FromFile(_path);
                var dimension = new FrameDimension(gifImg.FrameDimensionsList[0]);

                // Open a Stream and decode a GIF image
                Stream imageStreamSource = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var decoder = new System.Windows.Media.Imaging.GifBitmapDecoder(
                    imageStreamSource, System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                    System.Windows.Media.Imaging.BitmapCacheOption.Default);

                var frameCount = Math.Max(gifImg.GetFrameCount(dimension), decoder.Frames.Count);
                _bitmap = new Bitmap(frameCount, targetHeight);

                for (int idx = 0; idx < frameCount; idx++)
                {
                    var f1 = decoder.Frames[idx];
                    var frame = Common.Imaging.ImageHelper.BitmapImageToBitmap<System.Windows.Media.Imaging.BmpBitmapEncoder>(f1);//new Bitmap(gifImg1);
                    CopyPixelData(_bitmap, frame, _points, idx);
                }
            }
            else
            {
                _bitmap = new Bitmap(5, targetHeight);
            }
            var size = GetSize(targetHeight);
            _binary = new List<byte>(new byte[size]);
        }

        protected override void LoadFile(ushort targetHeight)
        {
            
        }

        private void CopyPixelData(Bitmap bitmap, Bitmap frame, List<Point> points, int frameIdx)
        {
            int row = 0;
            try
            {
                foreach (var point in points)
                {
                    var px = Color.Black;

                    if (point.X + _xOffset < frame.Width &&
                        point.Y + _yOffset < frame.Height &&
                        point.X + _xOffset >= 0 &&
                        point.Y + _yOffset >= 0)
                    {
                        px = frame.GetPixel(point.X + _xOffset, point.Y + _yOffset);
                    }
                    px = SetBrightness(px, Brightness);     // set brightness
                    px = ColorProfileWS2812.Modify(px);     // set color profile
                    bitmap.SetPixel(frameIdx, row, px);
                    row++;
                }
            }
            catch(Exception ex)
            {
                
            }
        }

        protected Color SetBrightness(Color c, byte brightness)
        {
            float colorMult = (float)brightness / 100;

            byte r = (byte)(c.R * colorMult);
            byte g = (byte)(c.G * colorMult);
            byte b = (byte)(c.B * colorMult);

            return Color.FromArgb(r, g, b);
        }
    }
}
