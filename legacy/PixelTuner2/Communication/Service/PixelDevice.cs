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
        #region Properties

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

        long _maxMemory;
        /// <summary>
        /// Доступный максимум памяти в байтах
        /// </summary>
        public long MaxMemory
        {
            get
            {
                if (_maxMemory > 3800000) _maxMemory = 3800000;
                if (_platformVersion == 2) return _maxMemory / 1024;
                else return _maxMemory;
            }
            set { _maxMemory = value; }
        }

        float _energyLevel = 0;
        /// <summary>
        /// Уровень заряда аккумулятора (1..100)
        /// </summary>
        public float EnergyLevel
        {
            get { return _energyLevel; }
            set
            {
                _energyLevel = value;
                PropertyChanged?.Invoke("EnergyLevel");
            }
        }

        ushort _ledsTotal = 0;
        /// <summary>
        /// Число диодов в устройстве
        /// </summary>
        public ushort LedsTotal
        {
            get { return _ledsTotal; }
            set { _ledsTotal = value; }
        }

        private string _tag;
        public string Tag
        {
            get { return _tag; }
            set
            {
                _tag = value;
                PropertyChanged?.Invoke("Tag");
            }
        }

        #endregion

        public PixelDevice(SerialPort port, bool runMonitoring = true)
            : base(port, runMonitoring)
        {
            this.WriteReadDelay = 0;
            this._fullName = "";
            this.Name = SupportedDevices.TryGetName("");
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

        public override void GetInfo()
        {
            lock (this._longOperation)
            {
                try
                {
                    List<byte> data = base.DoSession(new PixelDevicePacket(PixelDeviceCommand.INFO, null)).Data;
                    
                    PlatformVersion = data[0];
                    BootVersion = data[1];
                    if (Name.Contains("E-PIC_v4")) PlatformVersion = 4;
                    FirmwareVersion = data[2];
                    LedsTotal = (ushort)((data[3] << 8) | data[4]);
                    int num = (data[5] << 8) | data[6];
                    EnergyLevel = num;
                    byte[] memSizeArray = new byte[4] { data[10], data[9], data[8], data[7] };
                    MaxMemory = BitConverter.ToUInt32(memSizeArray, 0);
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
