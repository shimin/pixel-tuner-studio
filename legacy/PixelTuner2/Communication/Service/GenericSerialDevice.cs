using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;

namespace Communication.Service
{
    public class GenericSerialDevice : SerialDeviceBase
    {
        public GenericSerialDevice(SerialPort port, bool runMonitoring = true)
            : base(port)
        {

        }

        public override bool Busy
        {
            get { return true; }
            protected set { }
        }

        public override void GetInfo()
        {
            lock (this._longOperation)
            {
                base.Busy = true;
                base.SetStatus(ConnectionStatus.Downloading);
                try
                {
                    List<byte> data = base.DoSession(new PixelDevicePacket(PixelDeviceCommand.INFO, null)).Data;
                    PlatformVersion = data[0];
                    BootVersion = data[1];
                    FirmwareVersion = data[2];
                }
                catch (Exception exception)
                {
                    if (base.Logger != null)
                    {
                        base.Logger.WriteException(exception);
                    }
                }
                finally
                {
                    base.Busy = false;
                    base.SetStatus(ConnectionStatus.Online);
                }
            }
        }
    }
}
