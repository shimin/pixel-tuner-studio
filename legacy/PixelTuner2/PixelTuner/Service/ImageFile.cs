using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace PixelTuner.Service
{
    public class ImageFile
    {
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

        byte _index = 0;
        /// <summary>
        /// Индекс в файле проекта
        /// </summary>
        public byte Index
        {
            get { return _index; }
            set
            {
                _index = value;
                Update();
            }
        }

        List<byte> _binaryHeader = new List<byte>();
        /// <summary>
        /// Двоичное представление заголовка
        /// </summary>
        public List<byte> BinaryHeader
        {
            get
            {
                return _binaryHeader;
            }
            set { _binaryHeader = value; }
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
                Update();
            }
        }

        private void Update()
        {
            _binaryHeader = new List<byte>(Encoding.ASCII.GetBytes("PC"));
            _binaryHeader.Add(Index);
            _binaryHeader.AddRange(BitConverter.GetBytes(StartAddress));
            _binaryHeader.AddRange(BitConverter.GetBytes(Length));
        }

        private void UpdateBin()
        {
            try
            {
                Bitmap pic = (Bitmap)Image.FromFile(_path);
                _binaryData = BinarizePicture(pic);
                Update();
            }
            catch { _binaryData = new List<byte>(); }
        }

        private List<byte> BinarizePicture(Bitmap pic)
        {
            List<byte> result = new List<byte>();
            for (int column = 0; column < pic.Width; column++)
            {
                for (int row = 0; row < pic.Height; row++)
                {
                    Color c = pic.GetPixel(column, row);
                    result.Add(c.R);
                    result.Add(c.G);
                    result.Add(c.B);
                }
            }
            return result;
        }

        /// <summary>
        /// Длина в проекте
        /// </summary>
        public UInt32 Length
        {
            get { return (UInt32)_binaryData.Count; }
        }


        public ImageFile(string path)
        {
            Path = path;
        }

        public ImageFile(string path, byte index)
            : this(path)
        {
            Index = index;
        }
    }
}
