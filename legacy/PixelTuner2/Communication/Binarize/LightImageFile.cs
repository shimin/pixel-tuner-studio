using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace Communication.Binarize
{
    public class LightImageFile
    {
        #region Fields

        string _name;
        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        UInt32 _startAddress;
        public UInt32 StartAddress
        {
            get { return _startAddress; }
            set { _startAddress = value; }
        }

        public DateTime StartTime
        {
            get;
            set;
        }

        public DateTime EndTime
        {
            get;
            set;
        }

        // null если это переход
        Color? _color = null;
        public Color? Color
        {
            get { return _color; }
            set { _color = value; }
        }

        public static int Size
        {
            get
            {
                return
                    sizeof(UInt32) + // duration
                    3 * sizeof(byte) + // step rgb
                    3 * sizeof(byte); // target color rgb
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Creates object
        /// </summary>
        /// <param name="path">path to bitmap file</param>
        /// <param name="stretching">column repeat count</param>
        /// <param name="duration">duration in 100ms intervals</param>
        public LightImageFile(string name, DateTime startTime, DateTime endTime)
        {
            _name = name;
            StartTime = startTime;
            EndTime = endTime;
        }

        #endregion

        /// <summary>
        /// returns color by time marker
        /// </summary>
        /// <param name="time">Must be within start and end time</param>
        /// <param name="startColor">Left bound color</param>
        /// <param name="endColor">Right bound color</param>
        /// <returns></returns>
        internal virtual IEnumerable<byte> GetColor(DateTime time, System.Drawing.Color startColor, System.Drawing.Color endColor)
        {
            List<byte> color = new List<byte>();
            int dR = endColor.R - startColor.R;
            int dG = endColor.G - startColor.G;
            int dB = endColor.B - startColor.B;

            double durationMS = (EndTime - StartTime).TotalMilliseconds;
            double timepointMS = (time - StartTime).TotalMilliseconds;

            // для линейного перехода, линейная пропорция
            int xR = (int)(dR * timepointMS / durationMS);
            int xG = (int)(dG * timepointMS / durationMS);
            int xB = (int)(dB * timepointMS / durationMS);

            color.Add((byte)(startColor.R + xR));
            color.Add((byte)(startColor.G + xG));
            color.Add((byte)(startColor.B + xB));

            return color;
        }

        internal static IEnumerable<byte> ColorToBinary(Color c)
        {
            List<byte> color = new List<byte>();

            color.Add((byte)c.R);
            color.Add((byte)c.G);
            color.Add((byte)c.B);

            return color;
        }
    }
}
