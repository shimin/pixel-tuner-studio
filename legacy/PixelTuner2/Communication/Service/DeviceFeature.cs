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
        
        private List<byte> _val;

        public List<byte> Val
        {
            get { return _val; }
            set { _val = value; }
        }

        //left major, right minor

        //UInt16 _val;
        //public UInt16 Val
        //{
        //    get { return _val; }
        //    set { _val = value; }
        //}
        
        public byte[] GetBytes()
        {
            var len = (ushort)_val.Count;
            var result = new List<byte>()
            {
                (byte) (_id & 0xff),
                (byte) ((_id >> 8) & 0xff),
                
                (byte) (len & 0xff),
                (byte) ((len >> 8) & 0xff)
            };
            result.AddRange(_val);

            return result.ToArray();
        }

        public DeviceFeature(UInt16 id, byte[] val)
        {
            _id = id;
            Val = new List<byte>(val);
        }

        public DeviceFeature(UInt16 id, byte val)
        {
            _id = id;
            Val = new List<byte> { val };
        }
    }
}
