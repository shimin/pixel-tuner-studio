using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;

namespace Communication.Service
{
    public class SimplePoiDevice : SerialDeviceBase
    {
        public SimplePoiDevice(SerialPort port)
            : base(port)
        {

        }
   
        #region Properties
		
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
            set { _energyLevel = value; }
        }

	    #endregion

        public override void GetInfo()
        {
            lock (this._longOperation)
            {
                //this.Busy = true;
                //base.SetStatus(ConnectionStatus.Downloading);
                try
                {
                    List<byte> data = base.DoSession(new PixelDevicePacket(PixelDeviceCommand.INFO, null)).Data;
                    int num = (data[4] << 8) | data[5];
                    PlatformVersion = data[0];
                    EnergyLevel = num;
                    BootVersion = data[1];
                    FirmwareVersion = data[2];
                    byte[] memSizeArray = new byte[4] { data[9], data[8], data[7], data[6] };
                    MaxMemory = BitConverter.ToUInt32(memSizeArray, 0);
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
    }
}
