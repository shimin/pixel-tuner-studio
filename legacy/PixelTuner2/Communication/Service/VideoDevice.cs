using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;

namespace Communication.Service
{
    public class VideoDevice : PixelDevice
    {
        public VideoDevice(SerialPort port, bool runMonitoring = true) : base(port, runMonitoring)
        {
        }

        public override void GetInfo()
        {
            lock (this._longOperation)
            {
                try
                {
                    List<byte> data = base.DoSession(new PixelDevicePacket(PixelDeviceCommand.INFO, null)).Data;
                    int num = (data[5] << 8) | data[6];
                    PlatformVersion = data[0];
                    EnergyLevel = num;
                    BootVersion = data[1];
                    FirmwareVersion = data[2];
                    LedsTotal = (ushort)((data[3] << 8) | data[4]);
                }
                catch (Exception exception)
                {
                    if (base.Logger != null)
                    {
                        base.Logger.WriteException(exception);
                    }
                    throw exception;
                }
                finally
                {
                    this.Busy = false;
                    SetStatus(ConnectionStatus.Online);
                }
            }
        }
    }
}
