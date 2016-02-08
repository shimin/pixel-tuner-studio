using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.Log;
using System.IO.Ports;

namespace Communication.Service
{
    public class LightMasterDevice : SerialDeviceBase
    {
        public LightMasterDevice(SerialPort port)
            : base(port)
        {
            WriteReadDelay = 5;
        }

        public override SerialDeviceInfo GetInfo()
        {
            lock (this._longOperation)
            {
                base.Busy = true;
                base.SetStatus(ConnectionStatus.Downloading);
                LightMasterDeviceInfo info = new LightMasterDeviceInfo();
                try
                {
                    List<byte> data = base.DoSession(new PixelDevicePacket(PixelDeviceCommand.INFO, null)).Data;
                    int num = (data[4] << 8) | data[5];
                    info.PlatformVersion = data[0];
                    info.EnergyLevel = num;
                    info.BootVersion = data[1];
                    info.FirmwareVersion = data[2];
                    info.ChannelsTotal = data[3];
                    info.BatteryLevel = BitConverter.ToUInt16(data.ToArray(), 4);
                    info.MemoryAvailable = BitConverter.ToUInt32(data.ToArray(), 6);
                    data.CopyTo(10, info.Mac, 0, 8);
                    info.PanID = BitConverter.ToUInt16(data.ToArray(), 18);
                    info.ChannelID = data[19];
                    data.CopyTo(20, info.ServerMac, 0, 8);
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

