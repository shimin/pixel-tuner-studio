using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Communication.Service
{
    public class DeviceSessionEventArgs : DeviceEventArgs
    {
        public List<byte> Data { get; private set; }

        public DeviceSessionEventArgs(DeviceBase d, List<byte> data)
            : base(d)
        {
            Data = data;
        }
    }
}
