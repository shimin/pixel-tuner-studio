using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Communication.Service;
using System.IO.Ports;

namespace ConnectionTester
{
    internal class PixelDeviceTest : PixelDevice
    {
        internal PixelDeviceTest(SerialPort port)
            : base(port)
        {

        }

        internal IList<byte> SendRequest(List<byte> r)
        {
            return DoSession(r.ToArray());
        }
    }
}
