using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Communication.Service
{
    public class DeviceFeature
    {
        UInt16 _id;
        public UInt16 ID
        {
            get { return _id; }
            set { _id = value; }
        }

        UInt16 _val;
        public UInt16 Val
        {
            get { return _val; }
            set { _val = value; }
        }

        public byte[] GetBytes()
        {
            byte[] result = new byte[4];
            result[0] = (byte)((_id >> 8) & 0xff);
            result[1] = (byte)(_id & 0xff);

            result[2] = (byte)((_val >> 8) & 0xff);
            result[3] = (byte)(_val & 0xff);
            return result;
        }

        public DeviceFeature(UInt16 id, UInt16 val)
        {
            _id = id;
            _val = val;
        }
    }
}
