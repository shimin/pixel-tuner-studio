using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.Log;
using System.IO.Ports;

namespace Communication.Service
{
    public class UdmxDevice : PixelDevice
    {
        private object _longOperation;

        public UdmxDevice(SerialPort port)
            : base(port)
        {
            this._longOperation = new object();
        }

        public UdmxDeviceInfo GetInfo()
        {
            lock (this._longOperation)
            {
                base.Busy = true;
                base.SetStatus(ConnectionStatus.Downloading);
                UdmxDeviceInfo info = new UdmxDeviceInfo();
                try
                {
                    List<byte> data = base.DoSession(new PixelDevicePacket(PixelDeviceCommand.INFO, null)).Data;
                    int num = (data[4] << 8) | data[5];
                    info.PlatformVersion = data[0];
                    info.EnergyLevel = num;
                    info.BootVersion = data[1];
                    info.FirmwareVersion = data[2];
                    info.LedsTotal = data[3];
                    int num2 = (((data[6] << 0x18) | (data[7] << 0x10)) | (data[8] << 8)) | data[9];
                    info.MaxMemory = (num2 / 0x186a0) * 0x186a0;
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

        public override string GetName(out bool bootMode)
        {
            lock (this._longOperation)
            {
                return base.GetName(out bootMode);
            }
        }

        public override void Initialize(object parameter)
        {
            bool flag;
            base.Initialize(parameter);
            base.Name = this.GetName(out flag);
            base.BootMode = flag;
            this.Info = this.GetInfo();
            base.SetStatus(ConnectionStatus.Online);
        }

        public UdmxDeviceInfo Info { get; private set; }
    }
}

