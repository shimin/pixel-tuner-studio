using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Collections.ObjectModel;
using System.Management;
using Common.ViewModel;
using Common.Log;
using System.Drawing;

namespace Communication.Service
{
    using sp = Communication.Pixelate;

    public class PixelDevice : SerialDeviceBase
    {
        private string _fullName;
        public string FullName
        {
            get
            {
                return this._fullName;
            }
            set
            {
                this._fullName = value;
            }
        }

        public PixelDevice(SerialPort port)
            : base(port)
        {
            this.WriteReadDelay = 0;
            this._fullName = "";
            this.Name = SupportedDevices.TryGetName("");
        }

        public static int EstimateRequestsCount(int transmissionDataSize)
        {
            return ((transmissionDataSize / UsualPacketSize) + (((transmissionDataSize % UsualPacketSize) == 0) ? 2 : 3));
        }

        public override SerialDeviceInfo GetInfo()
        {
            lock (this._longOperation)
            {
                this.Busy = true;
                base.SetStatus(ConnectionStatus.Downloading);
                PixelDeviceInfo info = new PixelDeviceInfo();
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
                    this.Busy = false;
                    base.SetStatus(ConnectionStatus.Online);
                }
                return info;
            }
        }

        public virtual void WriteSettings(List<DeviceFeature> settings)
        {
            lock (this._longOperation)
            {
                this.Busy = true;
                base.SetStatus(ConnectionStatus.Uploading);
                try
                {
                    List<byte> data = new List<byte>();
                    foreach (DeviceFeature feature in settings)
                    {
                        data.AddRange(feature.GetBytes());
                        if (data.Count >= 60)
                        {
                            base.DoSession(new PixelDevicePacket(PixelDeviceCommand.WRITE_SETTINGS, data));
                            data.Clear();
                        }
                    }
                    if (data.Count > 0)
                    {
                        base.DoSession(new PixelDevicePacket(PixelDeviceCommand.WRITE_SETTINGS, data));
                        data.Clear();
                    }
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
                    this.Busy = false;
                    base.SetStatus(ConnectionStatus.Online);
                }
            }
        }

        public void SetColor(Color c, byte ledIndex)
        {
            lock (this._longOperation)
            {
                this.Busy = true;
                try
                {
                    PixelDevicePacket request = new PixelDevicePacket(PixelDeviceCommand.WHO_IS_IT, null);
                    List<byte> rawData = new List<byte> {
                        0x52,
                        0x07,
                        ledIndex
                    };
                    var cp = new sp.ColorProfile();
                    rawData.AddRange(Map8To12(c, sp.ImagesCollectionItem.ColorComponent.R, cp));
                    rawData.AddRange(Map8To12(c, sp.ImagesCollectionItem.ColorComponent.G, cp));
                    rawData.AddRange(Map8To12(c, sp.ImagesCollectionItem.ColorComponent.R, cp));
                    request = new PixelDevicePacket(rawData);
                    PixelDevicePacket packet2 = base.DoSession(request);
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
                    this.Busy = false;
                    base.SetStatus(ConnectionStatus.Online);
                }
            }
        }

        private IList<byte> Map8To12(Color c, sp.ImagesCollectionItem.ColorComponent cc, sp.ColorProfile cp)
        {
            UInt16 scaledValue = 0;
            switch (cc)
            {
                case sp.ImagesCollectionItem.ColorComponent.R: scaledValue = cp.R[c.R]; break;
                case sp.ImagesCollectionItem.ColorComponent.G: scaledValue = cp.G[c.G]; break;
                case sp.ImagesCollectionItem.ColorComponent.B: scaledValue = cp.B[c.B]; break;
            }
            return BitConverter.GetBytes(scaledValue);
        }

        #region Enumeration

        public override bool Equals(object obj)
        {
            if (obj is PixelDevice)
            {
                return ((obj as PixelDevice).PortName == base.PortName);
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return this.Name;
        }

        #endregion
    }
}
