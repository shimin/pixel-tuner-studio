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
        const int ParametersFieldSize = 1;

        #region Fields

        public string Key
        {
            get
            {
                return string.IsNullOrEmpty(Path) ? null : Path + "_" + Brightness.ToString();
            }
        }

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

        public byte Brightness { get; set; }

        UInt32 _startAddress;
        /// <summary>
        /// Адрес начала картинки. Влияет на поле EndAddress.
        /// </summary>
        public UInt32 StartAddress
        {
            get { return _startAddress + ParametersFieldSize; }
            set
            {
                _startAddress = value;
                if (_binary != null && _binary.Count > 3 + ParametersFieldSize)
                {
                    // 4 bytes of end address in binary representation
                    var bytes = BitConverter.GetBytes((UInt32)(_startAddress + _binary.Count - ParametersFieldSize));
                    _binary[0 + ParametersFieldSize] = bytes[0];
                    _binary[1 + ParametersFieldSize] = bytes[1];
                    _binary[2 + ParametersFieldSize] = bytes[2];
                    _binary[3 + ParametersFieldSize] = bytes[3];
                }
            }
        }

        /// <summary>
        /// Адрес конца картинки. Вычисляется на основе StartAddress.
        /// </summary>
        public UInt32 EndAddress
        {
            get { return _binary == null ? _startAddress : (UInt32)(_startAddress + _binary.Count - ParametersFieldSize); }
        }

        /// <summary>
        /// Фактическая длина бинарной части.
        /// </summary>
        public UInt32 BinaryLength
        {
            get { return _binary == null ? 0 : (UInt32)_binary.Count; }
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
            }
            else
            {
                _bitmap = new Bitmap(1, targetHeight);
            }
            var size = GetSize(targetHeight);
            _binary = new List<byte>(new byte[size]);
        }

        #endregion

        #region Methods

        private void GetColumn(byte targetHeight, int column, List<Color> columnColors)
        {
            int firstLineOffset = targetHeight - Bitmap.Height;
            for (int row = 0; row < targetHeight; row++)
            {
                // fill black part that exceeds real bitmap height.
                if (row < targetHeight)
                {
                    if (row < firstLineOffset)
                    {
                        columnColors.Add(Color.Black);
                    }
                    else
                    {
                        columnColors.Add(Bitmap.GetPixel(column, row - firstLineOffset));
                    }
                }
                else
                    columnColors.Add(Color.Black);
            }
        }

        internal int GetSize(byte targetHeight)
        {
            int size = 0;
            if (Bitmap != null || File.Exists(_path))
            {
                if (Bitmap == null) _bitmap = Bitmap.FromFile(_path) as Bitmap;

                var parameters = new List<byte>();
                AttachParameters(parameters, _depth);
                size += parameters.Count;

                if (_depth == ColorDepth.Bit36)
                {
                    size += Bitmap.Width * targetHeight * 3 * 2;
                }
                else if (_depth == ColorDepth.Bit24)
                {
                    size += Bitmap.Width * targetHeight * 3;
                }
                else if (_depth == ColorDepth.Bit12)
                {
                    size += Bitmap.Width * (targetHeight % 2 == 0 ? targetHeight / 2 : (targetHeight + 1) / 2) * 3;
                }
                size += sizeof(UInt32);     // end address field
            }
            return size;
        }

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
            AttachParameters(binary, ColorDepth.Bit36);
            int binaryLength = Bitmap.Width * targetHeight * 3 * 2;         // 2 bytes on each color part
            binary.AddRange(BitConverter.GetBytes((UInt32)(StartAddress + binaryLength + 4)));
            for (int column = 0; column < Bitmap.Width; column++)
            {
                List<Color> columnColors = new List<Color>();
                GetColumn(targetHeight, column, columnColors);
                foreach (var c in columnColors) binary.AddRange(Map8To12(c.R));
                foreach (var c in columnColors) binary.AddRange(Map8To12(c.G));
                foreach (var c in columnColors) binary.AddRange(Map8To12(c.B));
            }
            return binary;
        }

        private List<byte> GetBinary24(byte targetHeight)
        {
            List<byte> binary = new List<byte>();
            AttachParameters(binary, ColorDepth.Bit24);
            int binaryLength = Bitmap.Width * targetHeight * 3;             // 1 byte on each color part
            binary.AddRange(BitConverter.GetBytes((UInt32)(StartAddress + binaryLength + 4)));
            for (int column = 0; column < Bitmap.Width; column++)
            {
                List<Color> columnColors = new List<Color>();
                GetColumn(targetHeight, column, columnColors);
                foreach (var c in columnColors) binary.Add(Map8To8(c.R));
                foreach (var c in columnColors) binary.Add(Map8To8(c.G));
                foreach (var c in columnColors) binary.Add(Map8To8(c.B));
            }
            return binary;
        }

        private List<byte> GetBinary12(byte targetHeight)
        {
            List<byte> binary = new List<byte>();
            AttachParameters(binary, ColorDepth.Bit12);
            // 0.5 bytes on each color part 
            int binaryLength = (Bitmap.Width * targetHeight * 3) / 2 + (Bitmap.Width * targetHeight * 3) % 2;
            binary.AddRange(BitConverter.GetBytes((UInt32)(StartAddress + binaryLength + 4)));
            for (int column = 0; column < Bitmap.Width; column++)
            {
                List<Color> columnColors = new List<Color>();
                GetColumn(targetHeight, column, columnColors);
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
                idx = 0;
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
                idx = 0;
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
            byte[] endAddress = BitConverter.GetBytes((UInt32)(_startAddress + binary.Count - ParametersFieldSize));
            binary[0 + ParametersFieldSize] = endAddress[0];
            binary[1 + ParametersFieldSize] = endAddress[1];
            binary[2 + ParametersFieldSize] = endAddress[2];
            binary[3 + ParametersFieldSize] = endAddress[3];
            return binary;
        }

        private IList<byte> Map8To12(byte p)
        {
            float colorMult = (float)Brightness / 100;
            p = (byte)(p * colorMult);
            return BitConverter.GetBytes((UInt16)(4095 * p / byte.MaxValue));
        }

        private byte Map8To8(byte p)
        {
            float colorMult = (float)Brightness / 100;
            p = (byte)(p * colorMult);
            return p;
        }

        private IList<byte> Map8To4(byte p)
        {
            float colorMult = (float)Brightness / 100;
            p = (byte)(p * colorMult);
            return BitConverter.GetBytes((byte)(15 * p / byte.MaxValue));
        }

        private static void AttachParameters(List<byte> binary, ColorDepth depth)
        {
            if (depth == ColorDepth.Bit12) { binary.Add(4); }
            else if (depth == ColorDepth.Bit24) { binary.Add(8); }
            else if (depth == ColorDepth.Bit36) { binary.Add(12); }
        }

        #endregion

    }
}
