using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace Communication.Pixelate
{
    public class ImagesCollectionFanItem : ImagesCollectionItem
    {
        private List<Point> _points;
        private int _xOffset = 0;
        private int _yOffset = 0;
        int rays = 6;
        int length = 14;

        public ImagesCollectionFanItem(string imagePath, ColorDepth depth)
            : base(imagePath, (UInt16)84, depth)
        {
        }
        
        protected override void LoadFile(ushort targetHeight)
        {
            int columnHeight = rays * length;

            if (File.Exists(_path))
            {
                _bitmap = Bitmap.FromFile(_path) as Bitmap;
                int rayDistance = _bitmap.Width / rays;

                var b = new Bitmap(_bitmap.Width, columnHeight);
                int combinedColumnPixelIdx = 0;
                int normalizedWidth = (_bitmap.Width / rays) * rays;

                for (int idx = 0; idx < normalizedWidth; idx++)
                {
                    for (int ray = 0; ray < rays; ray++)
                    {
                        Color px = Color.Black;
                        if (ray % 2 == 0)
                            for (int row = length - 1; row >= 0; row--)
                            {
                                px = _bitmap.GetPixel((idx + ray * rayDistance) % normalizedWidth, row);
                                b.SetPixel(idx, combinedColumnPixelIdx, px);
                                combinedColumnPixelIdx = (combinedColumnPixelIdx + 1) % columnHeight;
                            }
                        else
                            for (int row = 0; row < length; row++)
                            {
                                px = _bitmap.GetPixel((idx + ray * rayDistance) % normalizedWidth, row);
                                b.SetPixel(idx, combinedColumnPixelIdx, px);
                                combinedColumnPixelIdx = (combinedColumnPixelIdx + 1) % columnHeight;
                            }
                    }
                }
                _bitmap = b;
            }
            else
            {
                _bitmap = new Bitmap(10, columnHeight);
            }
            var size = GetSize((ushort)columnHeight);
            _binary = new List<byte>(new byte[size]);
        }

        private void CopyPixelData(Bitmap bitmap, Bitmap frame, List<Point> points, int frameIdx)
        {
            int row = 0;
            try
            {
                foreach (var point in points)
                {
                    var px = Color.Black;
                    if (point.X < frame.Width && point.Y < frame.Height)
                        px = frame.GetPixel(point.X + _xOffset, point.Y + _yOffset);
                    //Console.WriteLine($"X={point.X}, Y={point.Y}");
                    bitmap.SetPixel(frameIdx, row, ColorProfileWS2812.Modify(px));
                    row++;
                }
            }
            catch (Exception ex)
            {

            }
        }
    }
}
