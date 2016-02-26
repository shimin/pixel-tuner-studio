using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Communication.Pixelate
{
    public class PixelImageFile
    {
        #region Fields

        public byte Brightness { get; set; }

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
            set { _path = value; }
        }

        UInt32 _startAddress;
        public UInt32 StartAddress
        {
            get { return _startAddress; }
            set { _startAddress = value; }
        }

        UInt16 _stretching;
        public UInt16 Stretching
        {
            get { return _stretching; }
            set { _stretching = value; }
        }

        UInt32 _duration;
        public UInt32 Duration
        {
            get { return _duration; }
            set { _duration = value; }
        }

        UInt32 _startTime = 0;
        /// <summary>
        /// Global start time in ms
        /// </summary>
        public UInt32 StartTime
        {
            get { return _startTime; }
            set { _startTime = value; }
        }

        UInt16 _mask = 0;
        /// <summary>
        /// Options such as laser (0 bit switches it on/off)
        /// </summary>
        public UInt16 Mask
        {
            get { return _mask; }
            set { _mask = value; }
        }

        public static int Size
        {
            get
            {
                return
                    sizeof(UInt32) +     // start address
                    2 * sizeof(UInt16) + // stretching and 2 bytes option
                    sizeof(UInt32);      // start time in ms
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
        public PixelImageFile(string path, UInt16 stretching, UInt32 duration, UInt32 startTime, UInt16 mask)
        {
            _path = path;
            _stretching = stretching;
            _duration = duration;
            _startTime = startTime;
            _mask = mask;
        }

        #endregion

    }
}
