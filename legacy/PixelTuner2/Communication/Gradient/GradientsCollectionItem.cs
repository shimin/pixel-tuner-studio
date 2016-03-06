using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace Communication.Gradient
{
    public class GradientsCollectionItem
    {
        const int ParametersFieldSize = 1;

        public enum ColorComponent
        {
            R,
            G,
            B
        }

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

        public GradientsCollectionItem(string imagePath, byte targetHeight = 1, ColorDepth depth = ColorDepth.Bit24)
        {
            _path = imagePath;
            _depth = depth;
            targetHeight = 1;
            if (System.IO.File.Exists(_path))
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

        internal int GetSize(byte targetHeight)
        {
            int size = 0;
            if (Bitmap != null || System.IO.File.Exists(_path))
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
                var cp = new ColorProfile();
                var c = Bitmap.GetPixel(column, 0);
                c = ApplyRestrictions(c);
                binary.AddRange(Map8To12(c, ColorComponent.R, cp)); // 400 mcd
                binary.AddRange(Map8To12(c, ColorComponent.G, cp)); // 800 mcd
                binary.AddRange(Map8To12(c, ColorComponent.B, cp)); // 200 mcd
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
                var c = Bitmap.GetPixel(column, 0);
                c = ApplyRestrictions(c);
                binary.Add(Map8To8(c, ColorComponent.R));
                binary.Add(Map8To8(c, ColorComponent.G));
                binary.Add(Map8To8(c, ColorComponent.B));
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
            
            int idx = 0;
            byte value = 0;

            for (int column = 0; column < Bitmap.Width; column++)
            {
                var c = Bitmap.GetPixel(column, 0);
                c = ApplyRestrictions(c);
                
                if (idx % 2 == 0) { value = (byte)((Map8To4(c, ColorComponent.R)[0] & 0x0f) << 4); }
                else
                {
                    value += (byte)(Map8To4(c, ColorComponent.R)[0] & 0x0f);
                    binary.Add(value);
                    value = 0;
                }
                idx++;

                if (idx % 2 == 0) { value = (byte)((Map8To4(c, ColorComponent.G)[0] & 0x0f) << 4); }
                else
                {
                    value += (byte)(Map8To4(c, ColorComponent.G)[0] & 0x0f);
                    binary.Add(value);
                    value = 0;
                }
                idx++;

                if (idx % 2 == 0) { value = (byte)((Map8To4(c, ColorComponent.B)[0] & 0x0f) << 4); }
                else
                {
                    value += (byte)(Map8To4(c, ColorComponent.B)[0] & 0x0f);
                    binary.Add(value);
                    value = 0;
                }
                idx++;
            }

            byte[] endAddress = BitConverter.GetBytes((UInt32)(_startAddress + binary.Count - ParametersFieldSize));
            binary[0 + ParametersFieldSize] = endAddress[0];
            binary[1 + ParametersFieldSize] = endAddress[1];
            binary[2 + ParametersFieldSize] = endAddress[2];
            binary[3 + ParametersFieldSize] = endAddress[3];
            return binary;
        }

        private IList<byte> Map8To12(Color c, ColorComponent cc, ColorProfile cp)
        {
            UInt16 scaledValue = 0;
            switch (cc)
            {
                case ColorComponent.R: scaledValue = cp.R[c.R]; break;
                case ColorComponent.G: scaledValue = cp.G[c.G]; break;
                case ColorComponent.B: scaledValue = cp.B[c.B]; break;
            }
            return BitConverter.GetBytes(scaledValue);
        }

        private byte Map8To8(Color c, ColorComponent cc)
        {
            byte result = 0;
            switch (cc)
            {
                case ColorComponent.R: result = c.R; break;
                case ColorComponent.G: result = c.G; break;
                case ColorComponent.B: result = c.B; break;
            }
            return result;
        }

        private IList<byte> Map8To4(Color c, ColorComponent cc)
        {
            byte result = 0;
            switch (cc)
            {
                case ColorComponent.R: result = c.R; break;
                case ColorComponent.G: result = c.G; break;
                case ColorComponent.B: result = c.B; break;
            }
            return BitConverter.GetBytes((byte)(15 * result / byte.MaxValue));
        }

        private Color ApplyRestrictions(Color c)
        {
            float colorMult = (float)Brightness / 100;

            byte r = (byte)(c.R * colorMult);
            byte g = (byte)(c.G * colorMult);
            byte b = (byte)(c.B * colorMult);

            return Color.FromArgb(r, g, b);
        }

        private static void AttachParameters(List<byte> binary, ColorDepth depth)
        {
            if (depth == ColorDepth.Bit12) { binary.Add(4); }
            else if (depth == ColorDepth.Bit24) { binary.Add(8); }
            else if (depth == ColorDepth.Bit36) { binary.Add(12); }
        }

        private class StringHelper
        {
            public static byte[] GetBytes(string str)
            {
                byte[] bytes = new byte[str.Length * sizeof(char)];
                System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
                return bytes;
            }

            public static string GetString(byte[] bytes)
            {
                char[] chars = new char[bytes.Length / sizeof(char)];
                System.Buffer.BlockCopy(bytes, 0, chars, 0, bytes.Length);
                return new string(chars);
            }
        }

        public static byte[] ImageToBytes(Bitmap src)
        {
            var rgbValues = new byte[0];

            System.Drawing.Imaging.BitmapData bd =
                src.LockBits(new Rectangle(0, 0, src.Width, src.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, src.PixelFormat);

            IntPtr ptr = bd.Scan0;
            int bytes = bd.Stride * src.Height;
            rgbValues = new byte[bytes];

            System.Runtime.InteropServices.Marshal.Copy(ptr, rgbValues, 0, rgbValues.Length);

            src.UnlockBits(bd);

            return rgbValues;
        }

        private void AttachSize(List<byte> binary, int cnt)
        {
            binary.AddRange(BitConverter.GetBytes((UInt32)(StartAddress + cnt + 4)));
        }

        #endregion
    }
}
