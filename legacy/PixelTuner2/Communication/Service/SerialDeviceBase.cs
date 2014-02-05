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
    public class SerialDeviceBase : DeviceBase
    {
        protected SerialPort _port = null;
        private string _portName = null;
        private ConnectionStatus _status = ConnectionStatus.Offline;
        private object _syncSession = new object();
        private int _writeReadDelay = 10;
        private ManagementEventWatcher w = null;

        public event DisconnectedDelegate Disconnected;

        public event SessionDelegate SessionFinished;

        public event SessionDelegate SessionStarted;

        public event StatusDelegate StatusChanged;

        public SerialDeviceBase(SerialPort port)
        {
            this._port = port;
            if (this._port != null)
            {
                this._portName = this._port.PortName;
                this._port.ReadTimeout = 0x7530;
                this._port.WriteTimeout = 0x7d0;
            }
            this.AddRemoveUSBHandler();
        }

        protected void AddRemoveUSBHandler()
        {
            ManagementScope scope = new ManagementScope(@"root\CIMV2")
            {
                Options = { EnablePrivileges = true }
            };
            try
            {
                WqlEventQuery query = new WqlEventQuery
                {
                    EventClassName = "__InstanceDeletionEvent",
                    WithinInterval = new TimeSpan(0, 0, 3),
                    Condition = "TargetInstance ISA 'Win32_USBControllerdevice'"
                };
                this.w = new ManagementEventWatcher(scope, query);
                this.w.EventArrived += new EventArrivedEventHandler(this.USBRemoved);
                this.w.Start();
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
                if (this.w != null)
                {
                    this.w.Stop();
                }
            }
        }

        public virtual void Disconnect()
        {
            lock (this._syncSession)
            {
                if (this._port != null)
                {
                    try
                    {
                        this._port.Close();
                        this._port = null;
                        this.SetStatus(ConnectionStatus.Offline);
                    }
                    catch
                    {
                    }
                }
            }
        }

        protected byte[] DoSession(byte[] query)
        {
            this.OnSessionStarted(this, new List<byte>(query));
            base.LogMessage("Request: " + Logger.BytesToString(query), MessageType.Debug);
            List<byte> data = new List<byte>();
            try
            {
                lock (this._syncSession)
                {
                    this._port.DiscardInBuffer();
                    this._port.DiscardOutBuffer();
                    this._port.Write(query, 0, query.Length);
                    Thread.Sleep(this.WriteReadDelay);
                    int num = -1;
                    try
                    {
                        num = this._port.ReadByte();
                        data.Add((byte)num);
                    }
                    catch
                    {
                        throw new TimeoutException("Packet ID reading timeout.");
                    }
                    if (num != query[0])
                    {
                        throw new ArgumentException(string.Format("Wrong answer {0} on request {1}.", num.ToString("X2"), query[0].ToString("X2")));
                    }
                    int count = -1;
                    try
                    {
                        count = this._port.ReadByte();
                        data.Add((byte)count);
                    }
                    catch
                    {
                        throw new TimeoutException("Packet length reading timeout.");
                    }
                    byte[] buffer = new byte[count];
                    int num3 = 0;
                    try
                    {
                        num3 = this._port.Read(buffer, 0, count);
                    }
                    catch
                    {
                    }
                    if (num3 != count)
                    {
                        throw new TimeoutException("Incorrect packet length " + num3.ToString() + " (expected " + count.ToString() + ")");
                    }
                    data.AddRange(buffer);
                    base.LogMessage("Result:  " + Logger.BytesToString(buffer), MessageType.Debug);
                }
            }
            catch (Exception exception)
            {
                throw exception;
            }
            finally
            {
                this.OnSessionFinished(this, data);
            }
            return data.ToArray();
        }

        protected PixelDevicePacket DoSession(PixelDevicePacket request)
        {
            return new PixelDevicePacket(new List<byte>(this.DoSession(request.RawData.ToArray())));
        }

        public virtual string GetName(out bool bootMode)
        {
            this.SetStatus(ConnectionStatus.Downloading);
            string str = "";
            bootMode = false;
            try
            {
                PixelDevicePacket request = new PixelDevicePacket(PixelDeviceCommand.WHO_IS_IT, null);
                PixelDevicePacket packet2 = this.DoSession(request);
                str = Encoding.ASCII.GetString(packet2.Data.ToArray());
                if (str.ToLower().Contains("boot"))
                {
                    bootMode = true;
                }
                base.LogMessage("Device name: " + str, MessageType.Info);
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
                this.SetStatus(ConnectionStatus.Online);
            }
            return str;
        }

        private void OnDisconnected()
        {
            if (this.Disconnected != null)
            {
                this.Disconnected(new EventArgs());
            }
        }

        private void OnSessionFinished(DeviceBase d, List<byte> data)
        {
            if (this.SessionFinished != null)
            {
                this.SessionFinished(new DeviceSessionEventArgs(d, data));
            }
        }

        private void OnSessionStarted(DeviceBase d, List<byte> data)
        {
            if (this.SessionStarted != null)
            {
                this.SessionStarted(new DeviceSessionEventArgs(d, data));
            }
        }

        private void OnStatusChanged()
        {
            if (this.StatusChanged != null)
            {
                this.StatusChanged(new EventArgs());
            }
        }

        protected void SetStatus(ConnectionStatus status)
        {
            this._status = status;
            this.OnStatusChanged();
        }

        private void USBRemoved(object sender, EventArgs e)
        {
            this.Disconnect();
            this.OnDisconnected();
        }

        public SerialPort Port
        {
            get
            {
                return this._port;
            }
            set
            {
                this._port = value;
            }
        }

        public string PortName
        {
            get
            {
                return this._portName;
            }
            set
            {
                this._portName = value;
            }
        }

        public ConnectionStatus Status
        {
            get
            {
                return this._status;
            }
        }

        public int WriteReadDelay
        {
            get
            {
                return this._writeReadDelay;
            }
            set
            {
                this._writeReadDelay = value;
            }
        }

        public delegate void DisconnectedDelegate(EventArgs e);

        public delegate void SessionDelegate(DeviceSessionEventArgs e);

        public delegate void StatusDelegate(EventArgs e);
    }
}
