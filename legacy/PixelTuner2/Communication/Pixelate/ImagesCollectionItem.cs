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
        const int ParametersFieldSize = 2;

        private enum ColorComponent
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
                foreach (var c in columnColors) binary.AddRange(Map8To12(c.R, ColorComponent.R)); // 400 mcd
                foreach (var c in columnColors) binary.AddRange(Map8To12(c.G, ColorComponent.G)); // 800 mcd
                foreach (var c in columnColors) binary.AddRange(Map8To12(c.B, ColorComponent.B)); // 200 mcd
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
                foreach (var c in columnColors) binary.Add(Map8To8(c.R, ColorComponent.R));
                foreach (var c in columnColors) binary.Add(Map8To8(c.G, ColorComponent.G));   
                foreach (var c in columnColors) binary.Add(Map8To8(c.B, ColorComponent.B));   
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

        private IList<byte> Map8To12(byte p, ColorComponent cc)
        {
            UInt16 scaledValue = calibrationTable[p];
            return BitConverter.GetBytes(scaledValue);
        }

        internal static UInt16[] calibrationTable = new UInt16[] {  0
                                                    ,1
                                                    ,2
                                                    ,3
                                                    ,4
                                                    ,5
                                                    ,6
                                                    ,7
                                                    ,8
                                                    ,9
                                                    ,10
                                                    ,11
                                                    ,12
                                                    ,13
                                                    ,14
                                                    ,15
                                                    ,16
                                                    ,17
                                                    ,18
                                                    ,19
                                                    ,20
                                                    ,21
                                                    ,23
                                                    ,25
                                                    ,27
                                                    ,29
                                                    ,31
                                                    ,33
                                                    ,35
                                                    ,37
                                                    ,39
                                                    ,41
                                                    ,43
                                                    ,45
                                                    ,47
                                                    ,49
                                                    ,51
                                                    ,53
                                                    ,55
                                                    ,63
                                                    ,71
                                                    ,79
                                                    ,87
                                                    ,95
                                                    ,103
                                                    ,111
                                                    ,119
                                                    ,127
                                                    ,135
                                                    ,143
                                                    ,151
                                                    ,159
                                                    ,167
                                                    ,175
                                                    ,183
                                                    ,191
                                                    ,199
                                                    ,207
                                                    ,215
                                                    ,223
                                                    ,231
                                                    ,239
                                                    ,247
                                                    ,255
                                                    ,263
                                                    ,271
                                                    ,279
                                                    ,287
                                                    ,295
                                                    ,303
                                                    ,311
                                                    ,319
                                                    ,327
                                                    ,335
                                                    ,343
                                                    ,351
                                                    ,359
                                                    ,367
                                                    ,375
                                                    ,383
                                                    ,399
                                                    ,415
                                                    ,431
                                                    ,447
                                                    ,463
                                                    ,479
                                                    ,495
                                                    ,511
                                                    ,527
                                                    ,543
                                                    ,559
                                                    ,575
                                                    ,591
                                                    ,607
                                                    ,623
                                                    ,639
                                                    ,655
                                                    ,671
                                                    ,687
                                                    ,703
                                                    ,719
                                                    ,735
                                                    ,751
                                                    ,767
                                                    ,783
                                                    ,799
                                                    ,815
                                                    ,831
                                                    ,847
                                                    ,863
                                                    ,879
                                                    ,895
                                                    ,911
                                                    ,927
                                                    ,943
                                                    ,959
                                                    ,975
                                                    ,991
                                                    ,1007
                                                    ,1023
                                                    ,1039
                                                    ,1055
                                                    ,1071
                                                    ,1087
                                                    ,1103
                                                    ,1119
                                                    ,1135
                                                    ,1151
                                                    ,1167
                                                    ,1183
                                                    ,1199
                                                    ,1215
                                                    ,1231
                                                    ,1247
                                                    ,1263
                                                    ,1279
                                                    ,1295
                                                    ,1311
                                                    ,1327
                                                    ,1343
                                                    ,1359
                                                    ,1375
                                                    ,1391
                                                    ,1407
                                                    ,1423
                                                    ,1439
                                                    ,1455
                                                    ,1471
                                                    ,1487
                                                    ,1503
                                                    ,1519
                                                    ,1535
                                                    ,1551
                                                    ,1567
                                                    ,1583
                                                    ,1599
                                                    ,1615
                                                    ,1631
                                                    ,1647
                                                    ,1663
                                                    ,1679
                                                    ,1695
                                                    ,1711
                                                    ,1727
                                                    ,1743
                                                    ,1759
                                                    ,1775
                                                    ,1791
                                                    ,1807
                                                    ,1823
                                                    ,1839
                                                    ,1855
                                                    ,1871
                                                    ,1887
                                                    ,1903
                                                    ,1919
                                                    ,1935
                                                    ,1951
                                                    ,1967
                                                    ,1983
                                                    ,1999
                                                    ,2015
                                                    ,2031
                                                    ,2047
                                                    ,2063
                                                    ,2079
                                                    ,2095
                                                    ,2111
                                                    ,2127
                                                    ,2143
                                                    ,2159
                                                    ,2175
                                                    ,2191
                                                    ,2207
                                                    ,2223
                                                    ,2239
                                                    ,2255
                                                    ,2271
                                                    ,2287
                                                    ,2303
                                                    ,2335
                                                    ,2367
                                                    ,2399
                                                    ,2431
                                                    ,2463
                                                    ,2495
                                                    ,2527
                                                    ,2559
                                                    ,2591
                                                    ,2623
                                                    ,2655
                                                    ,2687
                                                    ,2719
                                                    ,2751
                                                    ,2783
                                                    ,2815
                                                    ,2847
                                                    ,2879
                                                    ,2911
                                                    ,2943
                                                    ,2975
                                                    ,3007
                                                    ,3039
                                                    ,3071
                                                    ,3103
                                                    ,3135
                                                    ,3167
                                                    ,3199
                                                    ,3231
                                                    ,3263
                                                    ,3295
                                                    ,3327
                                                    ,3359
                                                    ,3391
                                                    ,3423
                                                    ,3455
                                                    ,3487
                                                    ,3519
                                                    ,3551
                                                    ,3583
                                                    ,3615
                                                    ,3647
                                                    ,3679
                                                    ,3711
                                                    ,3743
                                                    ,3775
                                                    ,3807
                                                    ,3839
                                                    ,3871
                                                    ,3903
                                                    ,3935
                                                    ,3967
                                                    ,3999
                                                    ,4031
                                                    ,4063
                                                    ,4095
        };

        private byte Map8To8(byte p, ColorComponent cc)
        {
            float colorMult = (float)Brightness / 100;
            p = (byte)(p * colorMult);
            if (cc == ColorComponent.G) p /= 2;
            return p;
        }

        private IList<byte> Map8To4(byte p)
        {
            float colorMult = (float)Brightness / 100;
            p = (byte)(p * colorMult);
            return BitConverter.GetBytes((byte)(15 * p / byte.MaxValue));
        }

        private static void AttachParameters(List<byte> binary, ColorDepth depth, bool laserOn)
        {
            if (laserOn) binary.Add(1);
            else binary.Add(0);

            if (depth == ColorDepth.Bit12) { binary.Add(4); }
            else if (depth == ColorDepth.Bit24) { binary.Add(8); }
            else if (depth == ColorDepth.Bit36) { binary.Add(12); }
        }

        #endregion

    }
}
