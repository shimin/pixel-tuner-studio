using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.IO;

namespace Communication.Pixelate
{
    public enum ColorDepth
    {
        Bit12 = 0,
        Bit24 = 1,
        Bit36 = 2
    }

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

        List<byte> _binary = null;

        ColorDepth _depth = ColorDepth.Bit24;

        #endregion

        #region Constructor

        public ImagesCollectionItem(string imagePath, byte targetHeight, ColorDepth depth)
        {
            _path = imagePath;
            _depth = depth;
            if (File.Exists(_path))
            {
                _bitmap = Bitmap.FromFile(_path) as Bitmap;
                GetBinary(targetHeight);
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Returns binary representation according to project format.
        /// </summary>
        /// <param name="targetHeight">Number of rows, height in pixels, LEDs on each side of poi.</param>
        /// <param name="vlueSize">Bits to store color value.</param>
        /// <returns></returns>
        public List<byte> GetBinary(byte targetHeight)
        {
            if (Bitmap != null)
            {
                _binary = new List<byte>();

                if (_depth == ColorDepth.Bit36)
                {
                    _binary = GetBinary36(targetHeight);
                }
                else if (_depth == ColorDepth.Bit24)
                {
                    _binary = GetBinary24(targetHeight);
                }
                else if (_depth == ColorDepth.Bit12)
                {
                    _binary = GetBinary12(targetHeight);
                }
            }

            return _binary;
        }

        private List<byte> GetBinary36(byte targetHeight)
        {
            List<byte> binary = new List<byte>();
            int binaryLength = Bitmap.Width * targetHeight * 3 * 2;         // 2 bytes on each color part
            binary.AddRange(BitConverter.GetBytes((UInt32)(StartAddress + binaryLength + 5)));
            binary.Add(12);
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
                foreach (var c in columnColors) binary.AddRange(Map8To12(c.R));
                foreach (var c in columnColors) binary.AddRange(Map8To12(c.G));
                foreach (var c in columnColors) binary.AddRange(Map8To12(c.B));
            }
            return binary;
        }

        private List<byte> GetBinary24(byte targetHeight)
        {
            List<byte> binary = new List<byte>();
            int binaryLength = Bitmap.Width * targetHeight * 3;             // 1 byte on each color part
            binary.AddRange(BitConverter.GetBytes((UInt32)(StartAddress + binaryLength + 5)));
            binary.Add(8);
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
                foreach (var c in columnColors) binary.Add(c.R);
                foreach (var c in columnColors) binary.Add(c.G);
                foreach (var c in columnColors) binary.Add(c.B);
            }
            return binary;
        }

        private List<byte> GetBinary12(byte targetHeight)
        {
            List<byte> binary = new List<byte>();
            // 0.5 bytes on each color part 
            int binaryLength = (Bitmap.Width * targetHeight * 3) / 2 + (Bitmap.Width * targetHeight * 3) % 2;
            binary.AddRange(BitConverter.GetBytes((UInt32)(StartAddress + binaryLength + 5)));
            binary.Add(4);
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
                byte value = 0;
                int idx = 0;
                foreach (var c in columnColors)
                {
                    if (idx == 0) { value = (byte)((Map8To4(c.R)[0] & 0x0f) << 4); idx++; }
                    else
                    {
                        value += (byte)(Map8To4(c.R)[0] & 0x0f);
                        binary.Add(value);
                        idx = 0;
                    }
                }
                if (idx != 0) binary.Add(value);
                foreach (var c in columnColors)
                {
                    if (idx == 0) { value = (byte)((Map8To4(c.G)[0] & 0x0f) << 4); idx++; }
                    else
                    {
                        value += (byte)(Map8To4(c.G)[0] & 0x0f);
                        binary.Add(value);
                        idx = 0;
                    }
                }
                if (idx != 0) binary.Add(value);
                foreach (var c in columnColors)
                {
                    if (idx == 0) { value = (byte)((Map8To4(c.B)[0] & 0x0f) << 4); idx++; }
                    else
                    {
                        value += (byte)(Map8To4(c.B)[0] & 0x0f);
                        binary.Add(value);
                        idx = 0;
                    }
                }
                if (idx != 0) binary.Add(value);
            }
            byte[] length = BitConverter.GetBytes((UInt32)(StartAddress + binary.Count + 4));
            binary[0] = length[0];
            binary[1] = length[1];
            binary[2] = length[2];
            binary[3] = length[3];
            return binary;
        }

        private IList<byte> Map8To12(byte p)
        {
            return BitConverter.GetBytes((UInt16)(4095 * p / byte.MaxValue));
        }

        private IList<byte> Map8To4(byte p)
        {
            return BitConverter.GetBytes((byte)(15 * p / byte.MaxValue));
        }

        #endregion
    }
}
