using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Communication.Binarize
{
    public abstract class LightFileBase
    {
        #region Properties

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
        public LightFileBase(string name, DateTime startTime, DateTime endTime)
        {
            _name = name;
            StartTime = startTime;
            EndTime = endTime;
        }

        #endregion

        internal static IEnumerable<byte> ColorToBinary(System.Drawing.Color c)
        {
            List<byte> color = new List<byte>();

            color.Add((byte)c.R);
            color.Add((byte)c.G);
            color.Add((byte)c.B);

            return color;
        }

        public abstract List<byte> GetBinary(int rate);
    }
}
