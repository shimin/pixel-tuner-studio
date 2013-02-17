using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Drawing;
using System.ComponentModel;
using System.IO;

namespace PixelTuner.Drawing
{
    public class ConfigurationItem
        : ConfigurationItemBase
    {
        public ConfigurationItem()
            :this (25, 80, 10)
        { }

        /// <summary>
        /// Создает объект-конфигурацию
        /// </summary>
        /// <param name="patternLines">Высота картинки, число квадратов по вертикали (соответствует числу диодов с одной стороны в палке)</param>
        /// <param name="patternLength">Длина картинки, число квадратов по горизонтали</param>
        /// <param name="pointSize">Размер квадрата</param>
        public ConfigurationItem(int patternLines, int patternLength, int pointSize)
            : base("")
        {
            this.PatternLines = patternLines;
            this.PatternLength = patternLength;
            this.PointSize = pointSize;
            MakeImage();            
        }

        private int _patternLines = 25;
        /// <summary>
        /// Высота узора в светодиодах
        /// </summary>
        public int PatternLines
        {
            get { return _patternLines; }
            set { _patternLines = value; }
        }

        /// <summary>
        /// Константа - мминимальная длина узора
        /// </summary>
        public const int MinPatternLength = 20;

        /// <summary>
        /// Константа - максимальная длина узора
        /// </summary>
        public const int MaxPatternLength = 800;

        public const int MaxPatternLines = 100;

        private int _patternLength = 80;
        /// <summary>
        /// Длина полотна в условных ячейках
        /// </summary>
        public int PatternLength
        {
            get { return _patternLength; }
            set 
            {
                _patternLength = value;
            }
        }

        int _pointSize = 10;
        /// <summary>
        /// Размер одной ячейки сетки в пикселах (используется для дизайна и рисования), пересчитывать при онресайзе
        /// </summary>
        public int PointSize
        {
            get { return _pointSize; }
            set 
            {
                _pointSize = value;
            }
        }

        Image _image = null;
        public Image Image
        {
            get
            {
                return _image;
            }
            set
            {
                _image = value;
            }
        }

        /// <summary>
        /// Преобразовывает картинку из масштаба заданного PointSize в PointSize=1
        /// </summary>
        /// <returns>Возвращает картинку с масштабом PointSize=1</returns>
        public Image GetScaledImage()
        {
            if (_image == null) return null;
            Bitmap source = (Bitmap)_image;
            Bitmap result = new Bitmap(_image.Width / PointSize, _image.Height / PointSize);
            int xPos = 0;
            int yPos = 0;
            for (int x = 1; x < _image.Width; x += _pointSize)
            {
                for (int y = 1; y < _image.Height; y += _pointSize)
                {
                    result.SetPixel(xPos, yPos, source.GetPixel(x, y));
                    yPos++;
                }
                xPos++;
                yPos = 0;
            }
            return result;
        }

        /// <summary>
        /// Преобразовывает картинку из масштаба PointSize=1 в текущий заданный PointSize
        /// </summary>
        /// <param name="sourceImage">Картинка для преобразования</param>
        /// <param name="sourcePicturePrimary">Если истина, то обрезка выполняется по размеру входной картинки, 
        /// иначе - по размеру заданному параметрами PatternLength и PatternLines</param>
        public void SetScaledImage(Image sourceImage, bool sourcePicturePrimary = false)
        {
            if (_image == null) { this.Image = null; return; }

            Bitmap source = (Bitmap)sourceImage;
            if (sourcePicturePrimary)
            {
                int w = sourceImage.Width;
                int h = sourceImage.Height;
                if (sourceImage.Width > ConfigurationItem.MaxPatternLength) w = ConfigurationItem.MaxPatternLength;
                if (sourceImage.Height > ConfigurationItem.MaxPatternLines) h = ConfigurationItem.MaxPatternLines;
                this.Image = new Bitmap(w * this.PointSize, h * this.PointSize);
                this.PatternLength = w;
                this.PatternLines = h;
            }
            else
                this.Image = new Bitmap(this.PatternLength, this.PatternLines);

            int xPos = 0;
            int yPos = 0;
            var g = Graphics.FromImage(this.Image);
            for (int x = 0; x < sourceImage.Width; x++)
            {
                for (int y = 0; y < sourceImage.Height; y++)
                {
                    g.FillRectangle(new SolidBrush(source.GetPixel(x, y)), xPos, yPos, this.PointSize, this.PointSize);
                    yPos += this.PointSize;
                }
                xPos += this.PointSize;
                yPos = 0;
            }
            g.Dispose();
            sourceImage.Dispose();
        }

        private void MakeImage()
        {
            _image = new Bitmap(PatternLength * PointSize, PatternLines * PointSize);
            Graphics g = Graphics.FromImage(_image);
            g.FillRectangle(new SolidBrush(Color.Black), new Rectangle(0, 0, _image.Width, _image.Height));
            g.Dispose();
        }

        public override ConfigurationItemBase Clone()
        {
            ConfigurationItem newItem = new ConfigurationItem(this.PatternLines, this._patternLength, this.PointSize);
            newItem.Image = this.Image.Clone() as Image;
            return newItem;
        }
    }
}
