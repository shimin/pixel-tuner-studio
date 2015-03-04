using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.IO;
using Common.Imaging;

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
        const int ParametersFieldSize = 2;

        /// <summary>
        /// parameter to limit white color brightness
        /// </summary>
        const byte WTRSHLD = 200;

        /// <summary>
        /// apply restrictions after pixel amount
        /// </summary>
        const uint ApplyThresholdAfter = 65;

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

        UInt32 _startAddressCompressed;
        /// <summary>
        /// Адрес начала сжатой картинки. Влияет на поле EndAddress.
        /// </summary>
        public UInt32 StartAddressCompressed
        {
            get { return _startAddressCompressed + ParametersFieldSize; }
            set
            {
                _startAddressCompressed = value;
                if (_binaryCompressed != null && _binaryCompressed.Count > 3 + ParametersFieldSize)
                {
                    // 4 bytes of end address in binary representation
                    var bytes = BitConverter.GetBytes((UInt32)(_startAddressCompressed + _binaryCompressed.Count - ParametersFieldSize));
                    _binaryCompressed[0 + ParametersFieldSize] = bytes[0];
                    _binaryCompressed[1 + ParametersFieldSize] = bytes[1];
                    _binaryCompressed[2 + ParametersFieldSize] = bytes[2];
                    _binaryCompressed[3 + ParametersFieldSize] = bytes[3];
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

        /// <summary>
        /// Фактическая длина сжатой бинарной части.
        /// </summary>
        public UInt32 BinaryLengthCompressed
        {
            get { return _binaryCompressed == null ? 0 : (UInt32)_binaryCompressed.Count; }
        }

        List<byte> _binary = null;

        List<byte> _binaryCompressed = null;

        ColorDepth _depth = ColorDepth.Bit24;
        
        public bool LaserOn { get; set; }

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
                AttachParameters(parameters, _depth, LaserOn);
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
            AttachParameters(binary, ColorDepth.Bit36, LaserOn);
            int binaryLength = Bitmap.Width * targetHeight * 3 * 2;         // 2 bytes on each color part
            binary.AddRange(BitConverter.GetBytes((UInt32)(StartAddress + binaryLength + 4)));
            for (int column = 0; column < Bitmap.Width; column++)
            {
                List<Color> columnColors = new List<Color>();
                GetColumn(targetHeight, column, columnColors);
                var cp = new ColorProfile();
                if (targetHeight > 100)
                    cp = new ColorProfile3528();

                foreach (var c in columnColors) binary.AddRange(
                    Map8To12(ApplyRestrictions(targetHeight, c), ColorComponent.R, cp)); // 400 mcd

                foreach (var c in columnColors)
                    binary.AddRange(Map8To12(ApplyRestrictions(targetHeight, c), ColorComponent.G, cp)); // 800 mcd

                foreach (var c in columnColors)
                    binary.AddRange(Map8To12(ApplyRestrictions(targetHeight, c), ColorComponent.B, cp)); // 200 mcd
            }
            return binary;
        }

        private List<byte> GetBinary24(byte targetHeight)
        {
            List<byte> binary = new List<byte>();
            AttachParameters(binary, ColorDepth.Bit24, LaserOn);
            int binaryLength = Bitmap.Width * targetHeight * 3;             // 1 byte on each color part
            binary.AddRange(BitConverter.GetBytes((UInt32)(StartAddress + binaryLength + 4)));
            for (int column = 0; column < Bitmap.Width; column++)
            {
                List<Color> columnColors = new List<Color>();
                GetColumn(targetHeight, column, columnColors);
                foreach (var c in columnColors) binary.Add(Map8To8(ApplyRestrictions(targetHeight, c), ColorComponent.R));
                foreach (var c in columnColors) binary.Add(Map8To8(ApplyRestrictions(targetHeight, c), ColorComponent.G));
                foreach (var c in columnColors) binary.Add(Map8To8(ApplyRestrictions(targetHeight, c), ColorComponent.B));   
            }
            return binary;
        }

        private List<byte> GetBinary12(byte targetHeight)
        {
            List<byte> binary = new List<byte>();
            AttachParameters(binary, ColorDepth.Bit12, LaserOn);
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
                    if (idx == 0) { value = (byte)((Map8To4(ApplyRestrictions(targetHeight, c), ColorComponent.R)[0] & 0x0f) << 4); idx++; }
                    else
                    {
                        value += (byte)(Map8To4(ApplyRestrictions(targetHeight, c), ColorComponent.R)[0] & 0x0f);
                        binary.Add(value);
                        idx = 0;
                    }
                }
                if (idx != 0) binary.Add(value);
                idx = 0;
                foreach (var c in columnColors)
                {
                    if (idx == 0) { value = (byte)((Map8To4(ApplyRestrictions(targetHeight, c), ColorComponent.G)[0] & 0x0f) << 4); idx++; }
                    else
                    {
                        value += (byte)(Map8To4(ApplyRestrictions(targetHeight, c), ColorComponent.G)[0] & 0x0f);
                        binary.Add(value);
                        idx = 0;
                    }
                }
                if (idx != 0) binary.Add(value);
                idx = 0;
                foreach (var c in columnColors)
                {
                    if (idx == 0) { value = (byte)((Map8To4(ApplyRestrictions(targetHeight, c), ColorComponent.B)[0] & 0x0f) << 4); idx++; }
                    else
                    {
                        value += (byte)(Map8To4(ApplyRestrictions(targetHeight, c), ColorComponent.B)[0] & 0x0f);
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

        private IList<byte> Map8To12(Color c, ColorComponent cc, ColorProfile cp)
        {
            UInt16 scaledValue = 0;
            switch (cc)
            {
                case ColorComponent.R: scaledValue = cp.R[c.R]; break;
                case ColorComponent.G: scaledValue = cp.G[c.G]; break;
                case ColorComponent.B: scaledValue = cp.B[c.B]; break;
            }
            //UInt16 scaledValue = calibrationTable[p];
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

        private Color ApplyRestrictions(byte targetHeight, Color c)
        {
            float colorMult = (float)Brightness / 100;

            byte r = (byte)(c.R * colorMult);
            byte g = (byte)(c.G * colorMult);
            byte b = (byte)(c.B * colorMult);

            r = targetHeight > ApplyThresholdAfter && r > WTRSHLD && g > WTRSHLD && b > WTRSHLD ? WTRSHLD : r;
            g = targetHeight > ApplyThresholdAfter && r > WTRSHLD && g > WTRSHLD && b > WTRSHLD ? WTRSHLD : g;
            b = targetHeight > ApplyThresholdAfter && r > WTRSHLD && g > WTRSHLD && b > WTRSHLD ? WTRSHLD : b;

            return Color.FromArgb(r, g, b);
        }

        private static void AttachParameters(List<byte> binary, ColorDepth depth, bool laserOn)
        {
            if (laserOn) binary.Add(1);
            else binary.Add(0);

            if (depth == ColorDepth.Bit12) { binary.Add(4); }
            else if (depth == ColorDepth.Bit24) { binary.Add(8); }
            else if (depth == ColorDepth.Bit36) { binary.Add(12); }
        }

        public List<byte> GetBinaryCompressed(byte targetHeight)
        {
            if (Bitmap != null)
            {
                _binaryCompressed = new List<byte>();
                var data = new List<byte>();
                AttachDataCompressed(data);

                AttachParameters(_binaryCompressed, ColorDepth.Bit24, LaserOn);
                AttachSize(_binaryCompressed, data.Count);
                _binaryCompressed.AddRange(data);

                ////////////////////////////////////////////////
                // check back
                ////////////////////////////////////////////////
                List<UInt32> dataF = new List<UInt32>();
                for (int i = 0; i < data.Count - 1; i += 4)
                    dataF.Add(BitConverter.ToUInt32(new byte[] { data[i], data[i + 1], data[i + 2], data[i + 3] }, 0));

                var decompressedString = Decompress(dataF);
                var decompressedBytesHalves = Encoding.ASCII.GetBytes(decompressedString);
                var decompressedBytes = new byte[decompressedBytesHalves.Length];
                for (int i = 0; i < decompressedBytesHalves.Length; i++)
                    decompressedBytes[i] = (byte)(decompressedBytesHalves[i] * 2);
                File.WriteAllBytes("c:\\02_dec.bin", decompressedBytes.ToArray());

                Bitmap b = new Bitmap(Bitmap.Width, Bitmap.Height, Bitmap.PixelFormat);
                System.Drawing.Imaging.BitmapData bd = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), 
                    System.Drawing.Imaging.ImageLockMode.WriteOnly, b.PixelFormat);
                var dataTmp = ImageToBytes(Bitmap);
                System.Runtime.InteropServices.Marshal.Copy(decompressedBytes, 0, bd.Scan0, decompressedBytes.Length);
                b.UnlockBits(bd);
                b.Save("c:\\" + System.IO.Path.GetFileName(_path));
                ////////////////////////////////////////////////
            }
            return _binaryCompressed;
        }

        private void AttachDataCompressed(List<byte> binary)
        {
            var src = ImageToBytes(Bitmap);
            var srcHalves = new List<byte>();
            foreach (var b in src) { srcHalves.Add((byte)(b / 2)); }
            var srcString = Encoding.ASCII.GetString(srcHalves.ToArray());
            var compressed = Compress(srcString);
            compressed.ForEach((i) => { binary.AddRange(BitConverter.GetBytes(i)); });
            File.WriteAllBytes("c:\\01_src.bin", srcHalves.ToArray());
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

        #region LZW compression

        public static List<UInt32> Compress(string uncompressed)
        {
            // build the dictionary
            Dictionary<string, UInt32> dictionary = new Dictionary<string, UInt32>();
            for (UInt32 i = 0; i < 256; i++)
                dictionary.Add(((char)i).ToString(), i);

            string w = string.Empty;
            List<UInt32> compressed = new List<UInt32>();

            foreach (char c in uncompressed)
            {
                string wc = w + c;
                if (dictionary.ContainsKey(wc))
                {
                    w = wc;
                }
                else
                {
                    // write w to output
                    compressed.Add(dictionary[w]);
                    // wc is a new sequence; add it to the dictionary
                    dictionary.Add(wc, (UInt32)dictionary.Count);
                    w = c.ToString();
                }
            }

            // write remaining output if necessary
            if (!string.IsNullOrEmpty(w))
                compressed.Add(dictionary[w]);

            return compressed;
        }

        public static string Decompress(List<UInt32> compressed)
        {
            // build the dictionary
            Dictionary<UInt32, string> dictionary = new Dictionary<UInt32, string>();
            for (UInt32 i = 0; i < 256; i++)
                dictionary.Add(i, ((char)i).ToString());

            string w = dictionary[compressed[0]];
            compressed.RemoveAt(0);
            StringBuilder decompressed = new StringBuilder(w);

            foreach (UInt32 k in compressed)
            {
                string entry = null;
                if (dictionary.ContainsKey(k))
                    entry = dictionary[k];
                else if (k == dictionary.Count)
                    entry = w + w[0];

                decompressed.Append(entry);

                // new sequence; add it to the dictionary
                dictionary.Add((UInt32)dictionary.Count, w + entry[0]);

                w = entry;
            }

            return decompressed.ToString();
        }

        #endregion

        internal int GetCompressedSize(byte targetHeight)
        {
            int size = 0;
            if (Bitmap != null || File.Exists(_path))
            {
                if (Bitmap == null) _bitmap = Bitmap.FromFile(_path) as Bitmap;
                GetBinaryCompressed(targetHeight);
                size += _binaryCompressed.Count;
            }
            return size;
        }
    }
}
