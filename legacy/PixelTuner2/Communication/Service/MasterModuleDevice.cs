using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;

namespace Communication.Service
{
    public class MasterModuleDevice: SerialDeviceBase
    {
        public MasterModuleDevice(SerialPort port)
            : base(port)
        {

        }

        public override void Disconnect()
        {
            throw new NotImplementedException();
        }
    }
}
