using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace PixelTuner.Service
{
    public class ImageFile
    {
        public static UInt32 HeaderSize = 8;    //(адрес картинки=4 + коэффициент растяжения K=2 + время показа картинки M. Время=M * 0.1сек = 2)

        public static byte[] EndOfImagesBlock = new byte[] { 0x50, 0x43, 0xff };

        string _path = "";
        /// <summary>
        /// Путь к файлу на диске
        /// </summary>
        public string Path
        {
            get { return _path; }
            set
            {
                _path = value;
                UpdateBin();
            }
        }

        List<byte> _binaryData = new List<byte>();
        /// <summary>
        /// Двоичное представление картинки в формате проекта
        /// </summary>
        public List<byte> BinaryData
        {
            get 
            {
                return _binaryData;
            }
            private set 
            { 
                _binaryData = value; 
            }
        }

        UInt32 _startAddress = 0;
        /// <summary>
        /// Адрес начала
        /// </summary>
        public UInt32 StartAddress
        {
            get { return _startAddress; }
            set 
            { 
                _startAddress = value;
                if (Length > 4)     // если бинарная часть уже сформирована
                {                   // заменить 4 байта адреса 
                    _binaryData.RemoveRange(0, 4);
                    _binaryData.InsertRange(0, BitConverter.GetBytes(_startAddress + Length));
                }
            }
        }
        
        private void UpdateBin()
        {
            try
            {
                Bitmap pic = (Bitmap)Image.FromFile(_path);
                _binaryData = BinarizePicture(pic);
            }
            catch { _binaryData = new List<byte>(); }
        }

        private List<byte> BinarizePicture(Bitmap pic)
        {
            List<byte> result = new List<byte>();
            for (int column = 0; column < pic.Width; column++)
            {
                List<Color> columnColors = new List<Color>();
                for (int row = 0; row < _height; row++)
                {
                    if (row < pic.Height)                               // что за пределами реальной высоты заполнять черным.
                        columnColors.Add(pic.GetPixel(column, row));
                    else
                        columnColors.Add(Color.Black);
                }
                foreach (var c in columnColors) result.Add(c.R);
                foreach (var c in columnColors) result.Add(c.G);
                foreach (var c in columnColors) result.Add(c.B);
            }

            List<byte> bin = new List<byte>();
            bin.AddRange(BitConverter.GetBytes(StartAddress + Length));
            bin.AddRange(result);
            return bin;
        }

        /// <summary>
        /// Длина в байтах внутри карты памяти
        /// </summary>
        public UInt32 Length
        {
            get { return (UInt32)_binaryData.Count; }
        }

        private int _height = 0;
        /// <summary>
        /// Высота картинки заданная пользователем в проекте.
        /// </summary>
        public int Height
        {
            get { return _height; }
        }

        public ImageFile(UInt32 startAddress, string path, int height)
        {
            _startAddress = startAddress;
            _height = height;
            Path = path;
            UpdateBin();
        }
    }
}
