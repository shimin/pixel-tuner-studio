using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Communication.Service
{
    public class IlluminatorDeviceInfo : SerialDeviceInfo
    {
        byte[] _mac = new byte[8];
        /// <summary>
        /// 8-bytes mac address
        /// </summary>
        public byte[] Mac
        {
            get { return _mac; }
            set { _mac = value; }
        }

        UInt16 _panID;
        /// <summary>
        /// Network alias
        /// </summary>
        public UInt16 PanID
        {
            get { return _panID; }
            set { _panID = value; }
        }

        byte _channel;
        /// <summary>
        /// Wireless channel
        /// </summary>
        public byte Channel
        {
            get { return _channel; }
            set { _channel = value; }
        }
    }
}
