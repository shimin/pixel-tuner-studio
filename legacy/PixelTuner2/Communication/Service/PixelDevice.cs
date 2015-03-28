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
        private object _longOperation;

        public PixelDevice(SerialPort port)
            : base(port)
        {
            this._longOperation = new object();
            this.WriteReadDelay = 0;
            this._fullName = "dkjashfkjasdh";
            this.Name = PixelDeviceSpecs.TryGetName("");
        }

        public override bool Equals(object obj)
        {
            if (obj is PixelDevice)
            {
                return ((obj as PixelDevice).PortName == base.PortName);
            }
            return base.Equals(obj);
        }

        public static int EstimateRequestsCount(int transmissionDataSize)
        {
            return ((transmissionDataSize / UsualPacketSize) + (((transmissionDataSize % UsualPacketSize) == 0) ? 2 : 3));
        }

        private List<byte> GetCommandBytes(PixelDeviceCommand command, IList<byte> args = null)
        {
            List<byte> list = new List<byte> {
                (byte) command
            };
            switch (command)
            {
                case PixelDeviceCommand.WRITE_BLOCK:
                case PixelDeviceCommand.WRITE_SETTINGS:
                    if (args == null)
                    {
                        throw new ArgumentNullException("Dispatching bytes array wasn't specified", "args");
                    }
                    if (args.Count > UsualPacketSize)
                    {
                        throw new ArgumentOutOfRangeException(string.Format("Packet length is over maximum {0}", UsualPacketSize), "args");
                    }
                    list.Add((byte)args.Count);
                    list.AddRange(args);
                    break;

                case PixelDeviceCommand.END_WRITE_FLASH:
                    list.Add(0);
                    break;

                case PixelDeviceCommand.WRITE_IFLASH_MODE:
                    list.Add(0);
                    break;

                case PixelDeviceCommand.INFO:
                    list.Add(0);
                    break;

                case PixelDeviceCommand.SWITCH_TO_BOOT:
                    list.Add(0);
                    break;

                case PixelDeviceCommand.END_WRITE_DATA:
                    list.Add(0);
                    break;

                case PixelDeviceCommand.WRITE_IDATA_MODE:
                    list.Add((byte)args.Count);
                    list.AddRange(args);
                    break;

                case PixelDeviceCommand.WHO_IS_IT:
                    list.Add(0);
                    break;
            }
            list.Add(13);
            return list;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public PixelDeviceInfo GetInfo()
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
            this.Name = this.GetName(out flag);
            this.BootMode = flag;
            this.Info = this.GetInfo();
            base.SetStatus(ConnectionStatus.Online);
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

        public void SwitchToBoot()
        {
            lock (this._longOperation)
            {
                if (base.Port != null)
                {
                    try
                    {
                        base.Port.ReadTimeout = 500;
                        base.DoSession(new PixelDevicePacket(PixelDeviceCommand.SWITCH_TO_BOOT, null));
                        base.Port.ReadTimeout = 0x7530;
                    }
                    catch (Exception exception)
                    {
                        if (base.Logger != null)
                        {
                            base.Logger.WriteException(exception);
                        }
                    }
                }
            }
        }

        public override string ToString()
        {
            return this.Name;
        }

        public void WriteFile(List<byte> binaryData)
        {
            lock (this._longOperation)
            {
                this.Busy = true;
                base.SetStatus(ConnectionStatus.Uploading);
                bool noExceptions = false;
                try
                {
                    List<byte> data = new List<byte>();
                    var answer = base.DoSession(new PixelDevicePacket(PixelDeviceCommand.WRITE_IDATA_MODE, BitConverter.GetBytes(binaryData.Count)));
                    if (answer.Data[0] == 0x00) {
                        base.SetStatus(ConnectionStatus.UploadError);
                        this.Busy = false;
                        throw new Exception("Upload error");
                    }
                    foreach (byte num in binaryData)
                    {
                        data.Add(num);
                        if (data.Count == UsualPacketSize)
                        {
                            base.DoSession(new PixelDevicePacket(PixelDeviceCommand.WRITE_BLOCK, data));
                            data.Clear();
                        }
                    }
                    if (data.Count > 0)
                    {
                        base.DoSession(new PixelDevicePacket(PixelDeviceCommand.WRITE_BLOCK, data));
                        data.Clear();
                    }
                    base.DoSession(new PixelDevicePacket(PixelDeviceCommand.END_WRITE_DATA, null));
                    noExceptions = true;
                }
                catch (Exception exception)
                {
                    if (base.Logger != null)
                    {
                        base.Logger.WriteException(exception);
                        base.SetStatus(ConnectionStatus.UploadError);
                    }
                }
                finally
                {
                     this.Busy = false;
                    if (noExceptions)
                        base.SetStatus(ConnectionStatus.Online);
                }
            }
        }

        public void WriteFirmware(List<byte> binaryData)
        {
            lock (this._longOperation)
            {
                this.Busy = true;
                base.SetStatus(ConnectionStatus.Uploading);
                try
                {
                    List<byte> data = new List<byte>();
                    base.DoSession(new PixelDevicePacket(PixelDeviceCommand.WRITE_IFLASH_MODE, null));
                    foreach (byte num in binaryData)
                    {
                        data.Add(num);
                        if (data.Count == UsualPacketSize)
                        {
                            base.DoSession(new PixelDevicePacket(PixelDeviceCommand.WRITE_BLOCK, data));
                            data.Clear();
                        }
                    }
                    if (data.Count > 0)
                    {
                        base.DoSession(new PixelDevicePacket(PixelDeviceCommand.WRITE_BLOCK, data));
                        data.Clear();
                    }
                    List<byte> rawData = new PixelDevicePacket(PixelDeviceCommand.END_WRITE_FLASH, null).RawData;
                    base._port.Write(rawData.ToArray(), 0, rawData.Count);
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

        public void WriteSettings(List<DeviceFeature> settings)
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

        public bool BootMode { get; protected set; }

        public bool Busy { get; protected set; }

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

        public virtual PixelDeviceInfo Info { get; private set; }

        public string Name { get; set; }

        public static int UsualPacketSize
        {
            get
            {
                return 60;
            }
        }
    }
}
