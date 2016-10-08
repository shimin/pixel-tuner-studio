using Common.Log;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Management;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Communication.Service
{
    public abstract class SerialDeviceBase : DeviceBase, IDisposable
    {
        private Thread _portMonitor = null;

        public delegate void NotifyPropertyChanged(string propertyName);

        public NotifyPropertyChanged PropertyChanged = null;

        public static SerialDeviceBase Create(SerialPort p)
        {
            SerialDeviceBase dev = null;

            if (p != null && p.IsOpen)
            {
                byte[] buffer = GetNameBytes(p);
                string deviceName = Encoding.ASCII.GetString(buffer);

                var devName = SupportedDevices.TryGetType(deviceName);
                var devType = Type.GetType(devName);
                dev = Activator.CreateInstance(devType, p) as SerialDeviceBase;
                dev.Initialize(null);
            }

            return dev;
        }

        public static int EstimateRequestsCount(int transmissionDataSize)
        {
            return ((transmissionDataSize / UsualPacketSize) + (((transmissionDataSize % UsualPacketSize) == 0) ? 2 : 3));
        }

        private static byte[] GetNameBytes(SerialPort p)
        {
            p.DiscardInBuffer();
            p.DiscardOutBuffer();
            p.Write(new byte[] { 0x53, 0, 0x0d }, 0, 3);
            // reading of packet ID
            int num = -1;
            try
            {
                num = p.ReadByte();
                if (num != 0x53) throw new Exception();
            }
            catch { throw new TimeoutException("Device name request error."); }
            // reading of packet length
            int count = -1;
            try
            {
                count = p.ReadByte();
            }
            catch { throw new TimeoutException("Packet length reading timeout."); }
            // data reading
            byte[] buffer = new byte[count];
            int num3 = 0;
            try
            {
                num3 = p.Read(buffer, 0, count);
                if (num3 != count) throw new Exception();
            }
            catch { throw new TimeoutException("Device name reading timeout."); }
            return buffer;
        }

        #region Properties

        protected object _longOperation = new object();
        private object _syncSession = new object();        
        private ManagementEventWatcher w = null;

        protected SerialPort _port = null;
        public SerialPort Port
        {
            get
            {
                return _port;
            }
            set
            {
                _port = value;
            }
        }

        protected string _portName = null;
        public string PortName
        {
            get
            {
                return _portName;
            }
            set
            {
                _portName = value;
            }
        }

        private ConnectionStatus _status = ConnectionStatus.Offline;
        public ConnectionStatus Status
        {
            get
            {
                return _status;
            }
        }

        public int WriteReadDelay
        {
            get;
            set;
        }

        public bool BootMode { get; protected set; }

        private bool _busy = false;
        public bool Busy
        {
            get { return _busy; }
            protected set
            {
                _busy = value;
                PropertyChanged?.Invoke("Busy");
            }
        }

        public string Name { get; set; }

        public static int UsualPacketSize
        {
            get
            {
                return 60;
            }
        }

        protected int _firmwareVersion = 0;
        /// <summary>
        /// Версия прошивки
        /// </summary>
        public int FirmwareVersion
        {
            get { return _firmwareVersion; }
            set { _firmwareVersion = value; }
        }

        protected int _bootVersion = 0;
        /// <summary>
        /// Версия загрузчика
        /// </summary>
        public int BootVersion
        {
            get { return _bootVersion; }
            set { _bootVersion = value; }
        }

        protected int _platformVersion = 0;
        /// <summary>
        /// Версия платформы
        /// </summary>
        public int PlatformVersion
        {
            get { return _platformVersion; }
            set { _platformVersion = value; }
        }

        protected static int DefaultReadTimeout = 2000;

        protected static int DefaultWriteTimeout = 2000;

        #endregion

        #region Events

        public event DisconnectedDelegate Disconnected;

        public event SessionDelegate SessionFinished;

        public event SessionDelegate SessionStarted;

        public event StatusDelegate StatusChanged;

        private void OnDisconnected()
        {
            if (Disconnected != null)
            {
                Disconnected(new EventArgs());
            }
        }

        private void OnSessionFinished(DeviceBase d, List<byte> data)
        {
            if (SessionFinished != null)
            {
                SessionFinished(new DeviceSessionEventArgs(d, data));
            }
        }

        private void OnSessionStarted(DeviceBase d, List<byte> data)
        {
            if (SessionStarted != null)
            {
                SessionStarted(new DeviceSessionEventArgs(d, data));
            }
        }

        private void OnStatusChanged()
        {
            if (StatusChanged != null)
            {
                StatusChanged(new EventArgs());
            }
        }

        public delegate void DisconnectedDelegate(EventArgs e);

        public delegate void SessionDelegate(DeviceSessionEventArgs e);

        public delegate void StatusDelegate(EventArgs e);

        #endregion

        #region Enumeration

        public override bool Equals(object obj)
        {
            if (obj is SerialDeviceBase)
            {
                return ((obj as SerialDeviceBase).PortName == PortName);
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

        public SerialDeviceBase(SerialPort port)
        {
            Port = port;
            if (Port != null && Port.IsOpen)
            {
                _portName = _port.PortName;
                _port.ReadTimeout = DefaultReadTimeout;
                _port.WriteTimeout = DefaultWriteTimeout;
                GC.SuppressFinalize(_port.BaseStream);
            }
        }

        private void RunMonitoring()
        {
            _portMonitor = new Thread(() =>
            {
                while (Port.IsOpen)
                {
                    try
                    {
                        GetInfo();
                        Thread.Sleep(1000);
                    }
                    catch
                    {
                        Port.Dispose();
                    }
                }
            });
            _portMonitor.Start();
        }

        public abstract void GetInfo();

        protected void SetStatus(ConnectionStatus status)
        {
            _status = status;
            OnStatusChanged();
        }

        #region Core

        protected virtual byte[] DoSession(byte[] query)
        {
            OnSessionStarted(this, new List<byte>(query));
            LogMessage($"Request R({Port.ReadTimeout}) W{Port.WriteTimeout}: {Logger.BytesToString(query)}", MessageType.Debug);
            List<byte> data = new List<byte>();
            try
            {
                lock (this._syncSession)
                {
                    Port.DiscardInBuffer();
                    Port.DiscardOutBuffer();
                    Port.Write(query, 0, query.Length);

                    Thread.Sleep(WriteReadDelay);
                    // reading of packet ID
                    int num = -1;
                    try
                    {
                        num = Port.ReadByte();
                        data.Add((byte)num);
                    }
                    catch { throw new TimeoutException("Packet ID reading timeout."); }
                    if (num != query[0])
                    {
                        throw new ArgumentException($"Wrong answer {num:X2} on request {query[0]:X2}.");
                    }
                    // reading of packet length
                    int count = -1;
                    try
                    {
                        count = Port.ReadByte();
                        data.Add((byte)count);
                    }
                    catch { throw new TimeoutException("Packet length reading timeout."); }
                    // data reading
                    byte[] buffer = new byte[count];
                    int num3 = 0;
                    try
                    {
                        num3 = Port.Read(buffer, 0, count);
                    }
                    catch { throw new TimeoutException("Packet data reading timeout."); }
                    if (num3 != count)
                    {
                        throw new TimeoutException("Incorrect packet length " + num3.ToString() + " (expected " + count.ToString() + ")");
                    }
                    
                    data.AddRange(buffer);
                    LogMessage("Result:  " + Logger.BytesToString(buffer), MessageType.Debug);
                }
            }
            catch (Exception exception)
            {
                throw exception;
            }
            finally
            {
                OnSessionFinished(this, data);
            }
            return data.ToArray();
        }

        protected virtual PixelDevicePacket DoSession(PixelDevicePacket request)
        {
            byte[] answer = null;
            PixelDevicePacket result = null;
            try
            {
                answer = DoSession(request.RawData.ToArray());
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex);
                throw ex;
            }
            if (answer != null)
            {
                result = new PixelDevicePacket(answer);
            }
            return result;
        }

        #endregion

        #region Communication

        public override void Initialize(object parameter)
        {
            bool isBootMode;
            Name = GetName(out isBootMode);
            BootMode = isBootMode;
            GetInfo();
            SetStatus(ConnectionStatus.Online);
        }

        public virtual string GetName(out bool bootMode)
        {
            SetStatus(ConnectionStatus.Downloading);
            string str = "";
            bootMode = false;
            try
            {
                lock (this._longOperation)
                {
                    PixelDevicePacket request = new PixelDevicePacket(PixelDeviceCommand.WHO_IS_IT, null);
                    PixelDevicePacket packet2 = DoSession(request);
                    str = Encoding.ASCII.GetString(packet2.Data.ToArray());
                    if (str.ToLower().Contains("boot"))
                    {
                        bootMode = true;
                    }
                    LogMessage("Device name: " + str, MessageType.Info);
                }
            }
            catch (Exception exception)
            {
                LogException(exception);
            }
            finally
            {
                SetStatus(ConnectionStatus.Online);
            }
            return str;
        }

        public virtual void SwitchToBoot()
        {
            lock (this._longOperation)
            {
                if (Port != null)
                {
                    try
                    {
                        Port.ReadTimeout = 5000;
                        DoSession(new PixelDevicePacket(PixelDeviceCommand.SWITCH_TO_BOOT, null));
                        Port.ReadTimeout = DefaultReadTimeout;
                    }
                    catch (Exception exception)
                    {
                        LogException(exception);
                    }
                }
            }
        }

        public virtual void WriteSettings(List<DeviceFeature> settings)
        {
            lock (this._longOperation)
            {
                Busy = true;
                SetStatus(ConnectionStatus.Uploading);
                try
                {
                    Port.WriteTimeout = 30000;
                    Port.ReadTimeout = 30000;
                    var data = new List<byte>();
                    foreach (var feature in settings)
                    {
                        data.AddRange(feature.GetBytes());
                        if (data.Count < 60) continue;
                        DoSession(new PixelDevicePacket(PixelDeviceCommand.WRITE_SETTINGS, data));
                        data.Clear();
                    }
                    if (data.Count <= 0) return;
                    DoSession(new PixelDevicePacket(PixelDeviceCommand.WRITE_SETTINGS, data));
                    data.Clear();
                }
                catch (Exception exception)
                {
                    Logger?.WriteException(exception);
                }
                finally
                {
                    Busy = false;
                    SetStatus(ConnectionStatus.Online);
                    Port.ReadTimeout = DefaultReadTimeout;
                    Port.WriteTimeout = DefaultWriteTimeout;
                }
            }
        }

        public virtual void WriteFile(List<byte> binaryData)
        {
            lock (this._longOperation)
            {
                Busy = true;
                SetStatus(ConnectionStatus.Uploading);
                bool noExceptions = false;
                try
                {
                    Port.WriteTimeout = 30000;
                    Port.ReadTimeout = 30000;
                    List<byte> data = new List<byte>();
                    var answer = DoSession(new PixelDevicePacket(PixelDeviceCommand.WRITE_IDATA_MODE, BitConverter.GetBytes(binaryData.Count)));
                    if (answer.Data[0] == 0x00)
                    {
                        SetStatus(ConnectionStatus.UploadError);
                        Busy = false;
                        throw new Exception("Upload file error.");
                    }
                    foreach (byte num in binaryData)
                    {
                        data.Add(num);
                        if (data.Count == UsualPacketSize)
                        {
                            DoSession(new PixelDevicePacket(PixelDeviceCommand.WRITE_BLOCK, data));
                            data.Clear();
                        }
                    }
                    if (data.Count > 0)
                    {
                        DoSession(new PixelDevicePacket(PixelDeviceCommand.WRITE_BLOCK, data));
                        data.Clear();
                    }
                    DoSession(new PixelDevicePacket(PixelDeviceCommand.END_WRITE_DATA, null));
                    noExceptions = true;
                }
                catch (Exception exception)
                {
                    LogException(exception);
                    SetStatus(ConnectionStatus.UploadError);
                }
                finally
                {
                    Busy = false;
                    if (noExceptions)
                        SetStatus(ConnectionStatus.Online);
                    Port.ReadTimeout = DefaultReadTimeout;
                    Port.WriteTimeout = DefaultWriteTimeout;
                }
            }
        }

        public virtual void WriteFirmware(List<byte> binaryData)
        {
            lock (this._longOperation)
            {
                Busy = true;
                SetStatus(ConnectionStatus.Uploading);
                try
                {
                    Port.WriteTimeout = 30000;
                    Port.ReadTimeout = 30000;
                    List<byte> data = new List<byte>();
                    DoSession(new PixelDevicePacket(PixelDeviceCommand.WRITE_IFLASH_MODE, null));
                    foreach (byte num in binaryData)
                    {
                        data.Add(num);
                        if (data.Count == UsualPacketSize)
                        {
                            DoSession(new PixelDevicePacket(PixelDeviceCommand.WRITE_BLOCK, data));
                            data.Clear();
                        }
                    }
                    if (data.Count > 0)
                    {
                        DoSession(new PixelDevicePacket(PixelDeviceCommand.WRITE_BLOCK, data));
                        data.Clear();
                    }
                    List<byte> rawData = new PixelDevicePacket(PixelDeviceCommand.END_WRITE_FLASH, null).RawData;
                    Port.Write(rawData.ToArray(), 0, rawData.Count);
                }
                catch (Exception exception)
                {
                    LogException(exception);
                }
                finally
                {
                    Busy = false;
                    SetStatus(ConnectionStatus.Online);
                    Port.ReadTimeout = DefaultReadTimeout;
                    Port.WriteTimeout = DefaultWriteTimeout;
                }
            }
        }

        #endregion
        
        public virtual void Disconnect()
        {
            lock (this._syncSession)
            {
                if (Port != null)
                {
                    try
                    {
                        GC.ReRegisterForFinalize(_port);
                        Port.Close();
                        Port = null;
                        SetStatus(ConnectionStatus.Offline);
                        OnDisconnected();
                    }
                    catch
                    {
                    }
                }
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
