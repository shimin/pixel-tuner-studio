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

namespace Communication.Service
{
    public class PixelDevice : SerialDeviceBase
    {
        object _longOperation = new object();

        #region Static properties
        
        /// <summary>
        /// Maximum packet size in bytes.
        /// </summary>
        public static int UsualPacketSize { get { return 60; } }

        /// <summary>
        /// Calculates how many frames will be used to transmit some binary data.
        /// </summary>
        /// <param name="transmissionDataSize">Binary data size.</param>
        /// <returns>Frames count.</returns>
        public static int EstimateRequestsCount(int transmissionDataSize)
        {
            return (transmissionDataSize / UsualPacketSize) + (transmissionDataSize % UsualPacketSize == 0 ? 2 : 3);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Показывает, находится ли устройство в ржиме бутлоадера.
        /// </summary>
        public bool BootMode { get; private set; }

        public bool Busy { get; private set; }

        public string Name { get; set; }

        private string _fullName = "dkjashfkjasdh";
        public string FullName
        {
            get
            {
                return _fullName;
            }
            set
            {
                _fullName = value;
            }
        }

        public PixelDeviceInfo Info { get; private set; }

        #endregion
        
        #region Constructor

        public PixelDevice(SerialPort port)
            : base(port)
        {
            this.Name = PixelDeviceSpecs.TryGetName("");
            //procStatusInit = new InitProcessStatus((min, max, start, step) =>
            //    {
            //        ProgressMin = min;
            //        ProgressMax = max;
            //        Progress = start;
            //        Step = step;
            //    }
            //);
            //procPerformStep = new PerformStep(status =>
            //{
            //    this.Status = status;
            //    Progress += _step;
            //});
            //procSetStatus = new SetStatus(status => { this.Status = status; });
        }

        #endregion
        
        #region Protocol

        public override void Initialize(object parameter)
        {
            base.Initialize(parameter);
            bool bootModeFlag;
            Name = GetName(out bootModeFlag);
            BootMode = bootModeFlag;
            Info = GetInfo();
            SetStatus(ConnectionStatus.Online);
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
        /// Sets displaying color.
        /// </summary>
        public void SetColor(System.Drawing.Color c)
        {
            lock (_longOperation)
            {
                Busy = true;
                try
                {
                    PixelDevicePacket request = new PixelDevicePacket(PixelDeviceCommand.WHO_IS_IT);
                    
                    var buf = new List<byte>() { 0x52, 3, c.R, c.G, c.B };
                    request = new PixelDevicePacket(buf);
                    var answer = DoSession(request);
                }
                catch (Exception ex) { if (Logger != null) Logger.WriteException(ex); }
                finally { Busy = false; SetStatus(ConnectionStatus.Online); }
            }
        }

        /// <summary>
        /// Retreives device info.
        /// </summary>
        /// <returns>DeviceInfo structure.</returns>
        public PixelDeviceInfo GetInfo()
        {
            lock (_longOperation)
            {
                Busy = true;
                SetStatus(ConnectionStatus.Downloading);
                var result = new PixelDeviceInfo();
                try
                {
                    var buffer = DoSession(new PixelDevicePacket(PixelDeviceCommand.INFO)).Data;
                    int Uizm = (buffer[4] << 8 | buffer[5]);
                    result.PlatformVersion = buffer[0];
                    result.EnergyLevel = Uizm;
                    result.BootVersion = buffer[1];
                    result.FirmwareVersion = buffer[2];
                    result.LedsTotal = buffer[3];
                    int memoryBytes = buffer[6] << 24 | buffer[7] << 16 | buffer[8] << 8 | buffer[9]; // в байтах
                    result.MaxMemory = (memoryBytes / 100000) * 100000;
                }
                catch (Exception ex) { if (Logger != null) Logger.WriteException(ex); }
                finally { Busy = false; SetStatus(ConnectionStatus.Online); }
                return result;
            }
        }

        /// <summary>
        /// Forces device to disconnect and connect again in boot mode.
        /// </summary>
        public void SwitchToBoot()
        {
            lock (_longOperation)
            {
                if (this.Port == null) return;

                try
                {
                    this.Port.ReadTimeout = (int)PixelDeviceTimeout.Short;
                    DoSession(new PixelDevicePacket(PixelDeviceCommand.SWITCH_TO_BOOT));
                    this.Port.ReadTimeout = (int)PixelDeviceTimeout.Long;
                }
                catch (Exception ex) { if (Logger != null) Logger.WriteException(ex); }
            }
        }

        /// <summary>
        /// Writes binary data to device memory.
        /// </summary>
        /// <param name="binaryData">Data to write.</param>
        public void WriteFile(List<byte> binaryData)
        {
            lock (_longOperation)
            {
                Busy = true;
                SetStatus(ConnectionStatus.Uploading);
                try
                {
                    List<byte> buffer = new List<byte>();

                    // switch to write mode
                    DoSession(new PixelDevicePacket(PixelDeviceCommand.WRITE_IDATA_MODE, BitConverter.GetBytes(binaryData.Count)));

                    // write data
                    foreach (byte b in binaryData)
                    {
                        buffer.Add(b);
                        if (buffer.Count == UsualPacketSize)
                        {
                            DoSession(new PixelDevicePacket(PixelDeviceCommand.WRITE_BLOCK, buffer));
                            buffer.Clear();
                        }
                    }
                    if (buffer.Count > 0)
                    {
                        DoSession(new PixelDevicePacket(PixelDeviceCommand.WRITE_BLOCK, buffer));
                        buffer.Clear();
                    }

                    // quit from write mode
                    DoSession(new PixelDevicePacket(PixelDeviceCommand.END_WRITE_DATA));
                }
                catch (Exception ex) { if (Logger != null) Logger.WriteException(ex); }
                finally { Busy = false; SetStatus(ConnectionStatus.Online); }
            }
        }

        /// <summary>
        /// Writes firmware.
        /// </summary>
        /// <param name="binaryData">Firmware binary data.</param>
        public void WriteFirmware(List<byte> binaryData)
        {
            lock (_longOperation)
            {
                Busy = true;
                SetStatus(ConnectionStatus.Uploading);
                try
                {
                    List<byte> buffer = new List<byte>();

                    //int max = PixelDevice.EstimateRequestsCount(binaryData.Count);
                    //procStatusInit.Invoke(0, max);
                    //procPerformStep.Invoke(ConnectionStatus.Uploading);

                    // переход в режим записи
                    DoSession(new PixelDevicePacket(PixelDeviceCommand.WRITE_IFLASH_MODE));
                    //procPerformStep.Invoke(ConnectionStatus.Uploading);

                    // запись данных
                    foreach (byte b in binaryData)
                    {
                        buffer.Add(b);
                        if (buffer.Count == UsualPacketSize)
                        {
                            DoSession(new PixelDevicePacket(PixelDeviceCommand.WRITE_BLOCK, buffer));
                            //procPerformStep.Invoke(ConnectionStatus.Uploading);
                            buffer.Clear();
                        }
                    }
                    if (buffer.Count > 0)
                    {
                        DoSession(new PixelDevicePacket(PixelDeviceCommand.WRITE_BLOCK, buffer));
                        //procPerformStep.Invoke(ConnectionStatus.Uploading);
                        buffer.Clear();
                    }

                    // выход из режима записи
                    List<byte> finishProcess = (new PixelDevicePacket(PixelDeviceCommand.END_WRITE_FLASH)).RawData;
                    _port.Write(finishProcess.ToArray(), 0, finishProcess.Count);
                }
                catch (Exception ex) { if (Logger != null) Logger.WriteException(ex); }
                finally { Busy = false; SetStatus(ConnectionStatus.Online); }
            }
        }

        /// <summary>
        /// Writes list of device settings.
        /// </summary>
        /// <param name="settings">Settings list to write.</param>
        public void WriteSettings(List<DeviceFeature> settings)
        {
            lock (_longOperation)
            {
                Busy = true;
                SetStatus(ConnectionStatus.Uploading);
                try
                {
                    List<byte> args = new List<byte>();
                    foreach (DeviceFeature df in settings)
                    {
                        args.AddRange(df.GetBytes());
                        if (args.Count >= 60)
                        {
                            DoSession(new PixelDevicePacket(PixelDeviceCommand.WRITE_SETTINGS, args));
                            args.Clear();
                        }
                    }
                    if (args.Count > 0)
                    {
                        DoSession(new PixelDevicePacket(PixelDeviceCommand.WRITE_SETTINGS, args));
                        args.Clear();
                    }
                }
                catch (Exception ex) { if (Logger != null) Logger.WriteException(ex); }
                finally { Busy = false; SetStatus(ConnectionStatus.Online); }
            }
        }

        private List<byte> GetCommandBytes(PixelDeviceCommand command, IList<byte> args = null)
        {
            List<byte> result = new List<byte>() { (byte)command };
            switch (command)
            {
                case PixelDeviceCommand.INFO: result.Add(0); break;
                case PixelDeviceCommand.WHO_IS_IT: result.Add(0); break;
                case PixelDeviceCommand.WRITE_IDATA_MODE: result.Add((byte)args.Count); result.AddRange(args); break;
                case PixelDeviceCommand.WRITE_IFLASH_MODE: result.Add(0); break;
                case PixelDeviceCommand.END_WRITE_DATA: result.Add(0); break;
                case PixelDeviceCommand.END_WRITE_FLASH: result.Add(0); break;
                case PixelDeviceCommand.SWITCH_TO_BOOT: result.Add(0); break;
                case PixelDeviceCommand.WRITE_SETTINGS:
                case PixelDeviceCommand.WRITE_BLOCK: 
                    if (args == null) 
                        throw new ArgumentNullException("Dispatching bytes array wasn't specified", "args");
                    if (args.Count > UsualPacketSize)
                        throw new ArgumentOutOfRangeException(String.Format("Packet length is over maximum {0}", UsualPacketSize), "args");
                    result.Add((byte)args.Count);
                    result.AddRange(args);
                    break;
                default: break;
            }
            result.Add(0x0D);
            return result;
        }

        #endregion

        #region Misc

        public override string ToString()
        {
            return this.Name;
        }

        public override bool Equals(object obj)
        {
            if (obj is PixelDevice)
            {
                if ((obj as PixelDevice)._port != null && this._port != null)
                    if ((obj as PixelDevice)._port.PortName == this._port.PortName) return true;
                    else return false;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        #endregion
    }
}
