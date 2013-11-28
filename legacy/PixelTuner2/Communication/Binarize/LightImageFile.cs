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

    }
}
