using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.IO.Ports;

namespace Communication.Service
{
    public abstract class SerialDeviceBase : DeviceBase
    {
        #region Properties

        object _syncSession = new object();

        protected SerialPort _port = null;
        public SerialPort Port
        {
            get { return _port; }
            set { _port = value; }
        }

        int _writeReadDelay = 10;
        /// <summary>
        /// Задержка в мсиллисекундах между операцей записи и чтения.
        /// </summary>
        public int WriteReadDelay
        {
            get { return _writeReadDelay; }
            set { _writeReadDelay = value; }
        }

        ConnectionStatus _status = ConnectionStatus.Offline;
        public ConnectionStatus Status
        {
            get { return _status; }
        }

        #endregion
        
        #region Constructor
        
        public SerialDeviceBase(SerialPort port)
        {
            _port = port;
            if (_port != null)
            {
                _port.ReadTimeout = (int)PixelDeviceTimeout.Long;
                _port.WriteTimeout = 2000;
            }
            AddRemoveUSBHandler();
        }
        
        #endregion

        #region DisconnectWatcher

        private ManagementEventWatcher w = null;

        protected void AddRemoveUSBHandler()
        {
            WqlEventQuery q;
            ManagementScope scope = new ManagementScope("root\\CIMV2");
            scope.Options.EnablePrivileges = true;

            try
            {
                q = new WqlEventQuery();
                q.EventClassName = "__InstanceDeletionEvent";
                q.WithinInterval = new TimeSpan(0, 0, 3);
                q.Condition = "TargetInstance ISA 'Win32_USBControllerdevice'";
                w = new ManagementEventWatcher(scope, q);
                w.EventArrived += USBRemoved;

                w.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                if (w != null)
                {
                    w.Stop();
                }
            }
        }

        private void USBRemoved(object sender, EventArgs e)
        {
            this.Disconnect();
            OnDisconnected();
        }

        #endregion

        /// <summary>
        /// Returns device answer on user's query.
        /// </summary>
        /// <param name="query">Request bytes.</param>
        /// <exception cref="ArgumentException">Thrown when requested and received packet ID mismatch.</exception>
        /// <exception cref="TimeoutException">Thrown when ID, length or data reading timeout.</exception>
        /// <returns>Answer bytes.</returns>
        protected byte[] DoSession(byte[] query)
        {
            OnSessionStarted(this, new List<byte>(query));
            LogMessage("Request: " + BytesToString(query), Common.Log.MessageType.Debug);
            List<byte> received = new List<byte>();
            try
            {
                lock (_syncSession)
                {
                    _port.DiscardInBuffer();
                    _port.DiscardOutBuffer();
                    _port.Write(query, 0, query.Length);
                    Thread.Sleep(WriteReadDelay);

                    int packetID = -1;
                    try { packetID = _port.ReadByte(); received.Add((byte)packetID); }
                    catch { throw new TimeoutException("Packet ID reading timeout."); }

                    if (packetID != query[0])   // если идентификатор пакета неверный
                    {
                        throw new ArgumentException(
                            String.Format("Wrong answer {0} on request {1}.", packetID.ToString("X2"), query[0].ToString("X2")));
                    }

                    int len = -1;
                    try { len = _port.ReadByte(); received.Add((byte)len); }
                    catch { throw new TimeoutException("Packet length reading timeout."); }

                    byte[] receiveBuf = new byte[len];
                    int bytesGot = 0;
                    try { bytesGot = _port.Read(receiveBuf, 0, len); }
                    catch { }

                    if (bytesGot != len) throw new TimeoutException("Packet data reading timeout.");
                    received.AddRange(receiveBuf);
                    LogMessage("Result:  " + BytesToString(receiveBuf), Common.Log.MessageType.Debug);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                OnSessionFinished(this, received);
            }
            return received.ToArray();
        }

        protected PixelDevicePacket DoSession(PixelDevicePacket request)
        {
            return new PixelDevicePacket(new List<byte>(DoSession(request.RawData.ToArray())));
        }

        public abstract void Disconnect();

        protected void SetStatus(ConnectionStatus status)
        {
            _status = status;
            OnStatusChanged();
        }

        #region Events

        /// <summary>
        /// Occures when device disconnected.
        /// </summary>
        public event DisconnectedDelegate Disconnected;

        /// <summary>
        /// Occurs before sending bytes.
        /// </summary>
        public event SessionDelegate SessionFinished;

        /// <summary>
        /// Occurs after all bytes received or receive timeout.
        /// </summary>
        public event SessionDelegate SessionStarted;

        /// <summary>
        /// Occurs after all bytes received or receive timeout.
        /// </summary>
        public event StatusDelegate StatusChanged;

        public delegate void StatusDelegate(EventArgs e);

        public delegate void DisconnectedDelegate(EventArgs e);

        public delegate void SessionDelegate(DeviceSessionEventArgs e);

        private void OnStatusChanged()
        {
            if (StatusChanged != null)
                StatusChanged(new EventArgs());
        }

        private void OnDisconnected()
        {
            if (Disconnected != null)
                Disconnected(new EventArgs());
        }

        private void OnSessionStarted(DeviceBase d, List<byte> data)
        {
            if (SessionStarted != null)
                SessionStarted(new DeviceSessionEventArgs(d, data));
        }

        private void OnSessionFinished(DeviceBase d, List<byte> data)
        {
            if (SessionFinished != null)
                SessionFinished(new DeviceSessionEventArgs(d, data));
        }

        #endregion

    }
}
