using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
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
        private byte _brightness;
        int _rays = 6;
        int _length = 32;

        public ImagesCollectionFanItem(string imagePath, int rays, int length, ColorDepth depth, byte brightness)
            : base(imagePath, (UInt16)(rays * length), depth, brightness)
        {
            _rays = rays;
            _length = length;
            _brightness = brightness;
            LoadFile((UInt16)(rays * length));
        }

        /*protected override void LoadFile(ushort targetHeight)
        {
            int columnHeight = _rays * _length;

            if (File.Exists(_path))
            {
                _bitmap = Bitmap.FromFile(_path) as Bitmap;
                int rayDistance = _bitmap.Width / _rays;

                var width = _bitmap.Width%_rays == 0 ? _bitmap.Width/_rays : _bitmap.Width/_rays + 1;
                var b = new Bitmap(_bitmap.Width / _rays, columnHeight);
                int combinedColumnPixelIdx = 0;

                for (int idx = 0; idx < (_bitmap.Width / _rays) * _rays; idx += _rays)
                {
                    for (int ray = 0; ray < _rays; ray++)
                    {
                        Color px = Color.Black;
                        if (ray % 2 == 0)
                            for (int row = _length - 1; row >= 0; row--)
                            {
                                px = _bitmap.GetPixel(idx + ray, row);
                                px = SetBrightness(px, Brightness);        // set brightness
                                px = ColorProfileWS2812.Modify(px);         // set color profile
                                b.SetPixel(idx / _rays, combinedColumnPixelIdx, px);
                                combinedColumnPixelIdx = (combinedColumnPixelIdx + 1) % columnHeight;
                            }
                        else
                            for (int row = 0; row < _length; row++)
                            {
                                px = _bitmap.GetPixel(idx + ray, row);
                                px = SetBrightness(px, Brightness);        // set brightness
                                px = ColorProfileWS2812.Modify(px);         // set color profile
                                b.SetPixel(idx / _rays, combinedColumnPixelIdx, px);
                                combinedColumnPixelIdx = (combinedColumnPixelIdx + 1) % columnHeight;
                            }
                    }
                }
                _bitmap = b;
                _bitmap.Save(@"c:\test.bmp", ImageFormat.Bmp);
            }
            else
            {
                _bitmap = new Bitmap(10, columnHeight);
            }
            var size = GetSize((ushort)columnHeight);
            _binary = new List<byte>(new byte[size]);
        }*/

        protected override void LoadFile(ushort targetHeight)
        {
            int columnHeight = _rays * _length;

            if (File.Exists(_path))
            {
                _bitmap = Bitmap.FromFile(_path) as Bitmap;
                var b = new Bitmap(_bitmap.Width, columnHeight);
                int combinedColumnPixelIdx = 0;

                for (int column = 0; column < _bitmap.Width; column++)
                {
                    for (int ray = 0; ray < _rays; ray++)
                    {
                        Color px = Color.Black;
                        if (ray % 2 == 0)
                            for (int row = _length - 1; row >= 0; row--)
                            {
                                px = _bitmap.GetPixel(column, row);
                                px = SetBrightness(px, Brightness);        // set brightness
                                px = ColorProfileWS2812.Modify(px);         // set color profile
                                b.SetPixel(column, combinedColumnPixelIdx, px);
                                combinedColumnPixelIdx = (combinedColumnPixelIdx + 1) % columnHeight;
                            }
                        else
                            for (int row = 0; row < _length; row++)
                            {
                                px = _bitmap.GetPixel(column, row);
                                px = SetBrightness(px, Brightness);        // set brightness
                                px = ColorProfileWS2812.Modify(px);         // set color profile
                                b.SetPixel(column, combinedColumnPixelIdx, px);
                                combinedColumnPixelIdx = (combinedColumnPixelIdx + 1) % columnHeight;
                            }
                    }
                }
                _bitmap = b;
                _bitmap.Save(@"c:\test.bmp", ImageFormat.Bmp);
            }
            else
            {
                _bitmap = new Bitmap(10, columnHeight);
            }
            var size = GetSize((ushort)columnHeight);
            _binary = new List<byte>(new byte[size]);
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
