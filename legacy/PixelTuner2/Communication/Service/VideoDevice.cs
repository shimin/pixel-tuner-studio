using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;

namespace Communication.Service
{
    public class VideoDevice : PixelDevice
    {
        public VideoDevice(ITransport transport, bool runMonitoring = true) 
            : base(transport, runMonitoring)
        {
        }

        public override void GetInfo()
        {
            lock (this._longOperation)
            {
                try
                {
                    List<byte> data = base.DoSession(new PixelDevicePacket(PixelDeviceCommand.INFO, null)).Data;
                    PlatformVersion = data[0];
                    BootVersion = data[1];
                    FirmwareVersion = data[2];
                    LedsTotal = (ushort)((data[3] << 8) | data[4]);
                    EnergyLevel = (data[5] << 8) | data[6];
                    byte[] memSizeArray = new byte[4] { data[10], data[9], data[8], data[7] };
                    MaxMemory = BitConverter.ToUInt32(memSizeArray, 0);
                    Tag = Encoding.GetEncoding(1251).GetString(data.ToArray(), 45, 12).TrimEnd(new char[] {(char) 0});
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
