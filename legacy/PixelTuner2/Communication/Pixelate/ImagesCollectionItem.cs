using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.IO;

namespace Pixelate
{
    public class ImagesCollectionItem
    {
        #region Fields

        string _path;
        public string Path
        {
            get { return _path; }
        }

        Bitmap _bitmap;
        public Bitmap Bitmap
        {
            get { return _bitmap; }
        }

        UInt32 _startAddress;
        public UInt32 StartAddress
        {
            get { return _startAddress; }
            set
            {
                _startAddress = value;
                if (_binary != null && _binary.Count > 3)
                {
                    var bytes = BitConverter.GetBytes((UInt32)(StartAddress + _binary.Count));
                    _binary[0] = bytes[0];
                    _binary[1] = bytes[1];
                    _binary[2] = bytes[2];
                    _binary[3] = bytes[3];
                }
            }
        }

        public UInt32 EndAddress
        {
            get { return _binary == null ? _startAddress : (UInt32)(_startAddress + _binary.Count); }
        }

        private List<byte> _binary = null;

        #endregion

        #region Constructor

        public ImagesCollectionItem(string imagePath, byte targetHeight)
        {
            _path = imagePath;
            if (File.Exists(_path))
            {
                _bitmap = Bitmap.FromFile(_path) as Bitmap;
                GetBinary(targetHeight);
            }
        }

        #endregion

        #region Methods

        public List<byte> GetBinary(byte targetHeight)
        {
            if (Bitmap != null)
            {
                _binary = new List<byte>();

                int binaryLength = Bitmap.Width * targetHeight * 3;
                _binary.AddRange(BitConverter.GetBytes((UInt32)(StartAddress + binaryLength + 4)));

                for (int column = 0; column < Bitmap.Width; column++)
                {
                    List<Color> columnColors = new List<Color>();
                    for (int row = 0; row < targetHeight; row++)
                    {
                        // fill black part that exceeds real bitmap height.
                        if (row < Bitmap.Height)
                            columnColors.Add(Bitmap.GetPixel(column, row));
                        else
                            columnColors.Add(Color.Black);
                    }
                    foreach (var c in columnColors) _binary.Add(c.R);
                    foreach (var c in columnColors) _binary.Add(c.G);
                    foreach (var c in columnColors) _binary.Add(c.B);
                }

                List<byte> bin = new List<byte>();

                bin.AddRange(_binary);
            }
            else
            {
                _binary = null;
            }

            return _binary;
        }

        #endregion
    }
}
