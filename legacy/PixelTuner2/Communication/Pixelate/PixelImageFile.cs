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

        UInt16 _duration;
        public UInt16 Duration
        {
            get { return _duration; }
            set { _duration = value; }
        }

        public bool LaserOn { get; set; }

        public static int Size
        {
            get
            {
                return
                    sizeof(UInt32) +    // start address
                    2 * sizeof(UInt16); // duration and stretching
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
        public PixelImageFile(string path, UInt16 stretching, UInt16 duration)
        {
            _path = path;
            _stretching = stretching;
            _duration = duration;
        }

        #endregion

    }
}
