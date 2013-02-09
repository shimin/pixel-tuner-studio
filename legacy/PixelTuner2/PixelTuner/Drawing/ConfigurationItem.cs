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
            :this ("")
        { }

        public ConfigurationItem(string name)
            : base(name)
        {
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
        /// Константа - максимальная длина узора
        /// </summary>
        public const int MinPatternLength = 20;

        private int _patternLength = 80;
        /// <summary>
        /// Длина полотна в условных ячейках
        /// </summary>
        [XmlAttribute]
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
        [XmlAttribute]
        public int PointSize
        {
            get { return _pointSize; }
            set 
            {
                _pointSize = value;
            }
        }

        Image _image = null;
        [XmlIgnore]
        public Image Image
        {
            get
            {
                if (_image == null) MakeImage();
                return _image;
            }
            set
            {
                _image = value;
            }
        }

        #region Формирование данных согласно формату прошивки


        private void MakeImage()
        {

            _image = new Bitmap(PatternLength * PointSize, PatternLines * PointSize);
            Graphics g = Graphics.FromImage(_image);
            g.FillRectangle(new SolidBrush(Color.Black), new Rectangle(0, 0, _image.Width, _image.Height));
            g.Dispose();

        }

        #endregion

        public override ConfigurationItemBase Clone()
        {
            ConfigurationItem newItem = new ConfigurationItem(this.Name);
            newItem.Image = this.Image.Clone() as Image;
            newItem._patternLength = this._patternLength;
            newItem._pointSize = this._pointSize;
            return newItem;
        }
    }
}
