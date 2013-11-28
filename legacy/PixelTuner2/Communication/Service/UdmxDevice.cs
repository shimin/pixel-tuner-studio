using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.Log;
using System.IO.Ports;

namespace Communication.Service
{
    public class UdmxDevice : SerialDeviceBase
    {
        object _longOperation = new object();

        /// <summary>
        /// Показывает, находится ли устройство в ржиме бутлоадера.
        /// </summary>
        public bool BootMode { get; private set; }

        public bool Busy { get; private set; }

        public string Name { get; set; }

        public UdmxDeviceInfo Info { get; private set; }

        public UdmxDevice(SerialPort port)
            : base(port)
        {

        }

        public override void Initialize(object parameter)
        {
            base.Initialize(parameter);
            bool bootModeFlag;
            Name = GetName(out bootModeFlag);
            BootMode = bootModeFlag;
            Info = GetInfo();
            SetStatus(ConnectionStatus.Online);
        }

        #region Protocol

        /// <summary>
        /// Retreives device info.
        /// </summary>
        /// <returns>DeviceInfo structure.</returns>
        public UdmxDeviceInfo GetInfo()
        {
            lock (_longOperation)
            {
                Busy = true;
                SetStatus(ConnectionStatus.Downloading);
                var result = new UdmxDeviceInfo();
                try
                {
                    var buffer = DoSession(new PixelDevicePacket(PixelDeviceCommand.INFO)).Data;
                    int Uizm = (buffer[4] << 8 | buffer[5]);
                    //result.PlatformVersion = buffer[0];
                    //result.EnergyLevel = Uizm;
                    //result.BootVersion = buffer[1];
                    //result.FirmwareVersion = buffer[2];
                    //result.LedsTotal = buffer[3];
                    //int memoryBytes = buffer[6] << 24 | buffer[7] << 16 | buffer[8] << 8 | buffer[9]; // в байтах
                    //result.MaxMemory = (memoryBytes / 100000) * 100000;
                }
                catch (Exception ex) { if (Logger != null) Logger.WriteException(ex); }
                finally { Busy = false; SetStatus(ConnectionStatus.Online); }
                return result;
            }
        }

        /// <summary>
        /// Retrieves name from device.
        /// </summary>
        /// <returns>Name string.</returns>
        public string GetName(out bool bootMode)
        {
            lock (_longOperation)
            {
                SetStatus(ConnectionStatus.Downloading);
                string result = "";
                bootMode = false;
                Busy = true;
                try
                {
                    PixelDevicePacket request = new PixelDevicePacket(PixelDeviceCommand.WHO_IS_IT);
                    var answer = DoSession(request);
                    result = Encoding.ASCII.GetString(answer.Data.ToArray());

                    if (result.ToLower().Contains("boot")) bootMode = true;
                    LogMessage("Device name: " + result, MessageType.Info);
                }
                catch (Exception ex) { if (Logger != null) Logger.WriteException(ex); }
                finally { Busy = false; SetStatus(ConnectionStatus.Online); }
                return result;
            }
        }

        /// <summary>
        /// Simply releases port.
        /// </summary>
        override public void Disconnect()
        {
            lock (_longOperation)
            {
                if (_port != null)
                {
                    try
                    {
                        _port.Close();
                        _port = null;
                        SetStatus(ConnectionStatus.Offline);
                    }
                    catch { }
                }
            }
        }
    
        #endregion

    }
}
