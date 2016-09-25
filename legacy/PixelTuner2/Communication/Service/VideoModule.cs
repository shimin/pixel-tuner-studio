using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;

namespace Communication.Service
{
    public class VideoModule : PixelDevice
    {
        public VideoModule(SerialPort port) : base(port)
        {
        }
    }
}
