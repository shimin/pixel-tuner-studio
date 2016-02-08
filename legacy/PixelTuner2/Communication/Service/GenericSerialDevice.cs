using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;

namespace Communication.Service
{
    public class GenericSerialDevice : SerialDeviceBase
    {
        public GenericSerialDevice(SerialPort port)
            : base(port)
        {

        }

        public override SerialDeviceInfo GetInfo()
        {
            lock (this._longOperation)
            {
                base.Busy = true;
                base.SetStatus(ConnectionStatus.Downloading);
                SerialDeviceInfo info = new SerialDeviceInfo();
                try
                {
                    List<byte> data = base.DoSession(new PixelDevicePacket(PixelDeviceCommand.INFO, null)).Data;
                    info.PlatformVersion = data[0];
                    info.BootVersion = data[1];
                    info.FirmwareVersion = data[2];
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
                return info;
            }
        }
    }
}
