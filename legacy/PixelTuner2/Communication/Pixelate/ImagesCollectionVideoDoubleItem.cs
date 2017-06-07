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
            : base(imagePath1, (UInt16)(points1.Count + points2.Count), depth)
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

                Image gifImg2 = Image.FromFile(_path2);
                var dimension2 = new FrameDimension(gifImg2.FrameDimensionsList[0]);

                var frameCount1 = gifImg1.GetFrameCount(dimension1);
                var frameCount2 = gifImg2.GetFrameCount(dimension2);

                var frameCount = Math.Max(frameCount1, frameCount2);
                _bitmap = new Bitmap(frameCount, targetHeight1 + targetHeight2);

                for (int idx = 0; idx < frameCount; idx++)
                {
                    gifImg1.SelectActiveFrame(dimension1, idx % frameCount1);
                    gifImg2.SelectActiveFrame(dimension2, idx % frameCount2);
                    var frame1 = new Bitmap(gifImg1);
                    var frame2 = new Bitmap(gifImg2);
                    CopyPixelData(_bitmap, frame1, frame2, _points1, _points2, idx);
                }
                LastFrame1 = frameCount%frameCount1;
                LastFrame2 = frameCount%frameCount2;
            }
            else
            {
                _bitmap = new Bitmap(1, targetHeight1 + targetHeight2);
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
                    try
                    {
                        if (point.X + _xOffset1 < frame1.Width && point.Y + _yOffset1 < frame1.Height)
                            px = frame1.GetPixel(point.X + _xOffset1, point.Y + _yOffset1);
                    }
                    catch { }

                    bitmap.SetPixel(frameIdx, row, SetBrightness(px, _brightness1));
                    row++;
                }
                foreach (var point in points2)
                {
                    var px = Color.Black;
                    try
                    {
                        if (point.X + _xOffset2 < frame2.Width && point.Y + _yOffset2 < frame2.Height)
                            px = frame2.GetPixel(point.X + _xOffset2, point.Y + _yOffset2);
                    } catch { }
                    bitmap.SetPixel(frameIdx, row, SetBrightness(px, _brightness2));
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
