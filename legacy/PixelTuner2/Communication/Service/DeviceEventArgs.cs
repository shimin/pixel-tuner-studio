using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Communication.Service
{
    public class DeviceEventArgs
    {
        DeviceBase Device { get; set; }

        public DeviceEventArgs(DeviceBase device)
        {
            Device = device;
        }
    }
}
