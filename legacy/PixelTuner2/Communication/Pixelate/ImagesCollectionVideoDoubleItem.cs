using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace Communication.Pixelate
{
    public class ImagesCollectionVideoDoubleItem : ImagesCollectionItem
    {
        private List<Point> _points1;
        private int _xOffset1 = 0;
        private int _yOffset1 = 0;

        private List<Point> _points2;
        private int _xOffset2 = 0;
        private int _yOffset2 = 0;

        public int LastFrame1 = 0;
        public int LastFrame2 = 0;

        private byte _brightness1 = 100;
        private byte _brightness2 = 100;

        private string _path2 = "";

        public ImagesCollectionVideoDoubleItem(string imagePath1, string imagePath2,
            List<Point> points1, List<Point> points2, 
            int xOffset1, int yOffset1, int xOffset2, int yOffset2, ColorDepth depth, byte brightness1, byte brightness2)
            : base(imagePath1, (UInt16)(points1.Count + points2.Count), depth, 100, false)
        {
            _brightness1 = brightness1;
            _brightness2 = brightness2;
            _xOffset1 = xOffset1;
            _yOffset1 = yOffset1;
            _points1 = points1;
            _xOffset2 = xOffset2;
            _yOffset2 = yOffset2;
            _points2 = points2;

            _path2 = imagePath2;
            Load((ushort)points1.Count, (ushort)points2.Count);
        }

        private void Load(ushort targetHeight1, ushort targetHeight2)
        {
            if (File.Exists(_path) && File.Exists(_path2))
            {
                Image gifImg1 = Image.FromFile(_path);
                var dimension1 = new FrameDimension(gifImg1.FrameDimensionsList[0]);
                
                // Open a Stream and decode a GIF image
                Stream imageStreamSource = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var decoder = new System.Windows.Media.Imaging.GifBitmapDecoder(
                    imageStreamSource, System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                    System.Windows.Media.Imaging.BitmapCacheOption.Default);

                Stream imageStreamSource2 = new FileStream(_path2, FileMode.Open, FileAccess.Read, FileShare.Read);
                var decoder2 = new System.Windows.Media.Imaging.GifBitmapDecoder(
                    imageStreamSource2, System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                    System.Windows.Media.Imaging.BitmapCacheOption.Default);

                Image gifImg2 = Image.FromFile(_path2);
                var dimension2 = new FrameDimension(gifImg2.FrameDimensionsList[0]);

                var frameCount1 = Math.Max(gifImg1.GetFrameCount(dimension1), decoder.Frames.Count);
                var frameCount2 = Math.Max(gifImg2.GetFrameCount(dimension2), decoder2.Frames.Count);

                var frameCount = Math.Max(frameCount1, frameCount2);
                _bitmap = new Bitmap(frameCount, targetHeight1 + targetHeight2);

                for (int idx = 0; idx < frameCount; idx++)
                {
                    //gifImg1.SelectActiveFrame(dimension1, idx % frameCount1);
                    //gifImg2.SelectActiveFrame(dimension2, idx % frameCount2);
                    var f1 = decoder.Frames[idx % frameCount1];
                    var f2 = decoder2.Frames[idx % frameCount2];
                    var frame1 = Common.Imaging.ImageHelper.BitmapImageToBitmap<System.Windows.Media.Imaging.BmpBitmapEncoder>(f1);//new Bitmap(gifImg1);
                    var frame2 = Common.Imaging.ImageHelper.BitmapImageToBitmap<System.Windows.Media.Imaging.BmpBitmapEncoder>(f2); //new Bitmap(gifImg2);
                    CopyPixelData(_bitmap, frame1, frame2, _points1, _points2, idx);
                }
                LastFrame1 = frameCount%frameCount1;
                LastFrame2 = frameCount%frameCount2;
            }
            else
            {
                _bitmap = new Bitmap(5, targetHeight1 + targetHeight2);
            }
            var size = GetSize((ushort)(targetHeight1 + targetHeight2));
            _binary = new List<byte>(new byte[size]);
#if (DEBUG)
            string picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            var p = "bitmap_" + DateTime.Now.ToString("HH-mm-ss_ffff") + ".bmp";
            var savePath = System.IO.Path.Combine(picturesPath, p);
            _bitmap.Save(savePath);
#endif
        }

        protected override void LoadFile(ushort targetHeight)
        {

        }

        private void CopyPixelData(Bitmap bitmap, 
            Bitmap frame1, Bitmap frame2,
            List<Point> points1, List<Point> points2, int frameIdx)
        {
            int row = 0;
            try
            {
                foreach (var point in points1)
                {
                    var px = Color.Black;
                    if (point.X + _xOffset1 < frame1.Width &&
                        point.Y + _yOffset1 < frame1.Height &&
                        point.X + _xOffset1 >= 0 &&
                        point.Y + _yOffset1 >= 0)
                    {
                        px = frame1.GetPixel(point.X + _xOffset1, point.Y + _yOffset1);
                    }
                    px = SetBrightness(px, _brightness2);       // set brightness
                    px = ColorProfileWS2812.Modify(px);         // set color profile
                    bitmap.SetPixel(frameIdx, row, px);
                    row++;
                }
                foreach (var point in points2)
                {
                    var px = Color.Black;
                    if (point.X + _xOffset2 < frame2.Width &&
                        point.Y + _yOffset2 < frame2.Height &&
                        point.X + _xOffset2 >= 0 &&
                        point.Y + _yOffset2 >= 0)
                    {
                        px = frame2.GetPixel(point.X + _xOffset2, point.Y + _yOffset2);
                    }
                    px = SetBrightness(px, _brightness2);       // set brightness
                    px = ColorProfileWS2812.Modify(px);       // set color profile
                    bitmap.SetPixel(frameIdx, row, px);
                    row++;
                }
            }
            catch (Exception ex)
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
