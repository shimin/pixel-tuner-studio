using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;

namespace Communication.Service
{
    public class IlluminatorDevice : SerialDeviceBase
    {
        #region Properties
        
        byte[] _mac = new byte[8];
        /// <summary>
        /// 8-bytes mac address
        /// </summary>
        public byte[] Mac
        {
            get { return _mac; }
            set { _mac = value; }
        }

        UInt16 _panID;
        /// <summary>
        /// Network alias
        /// </summary>
        public UInt16 PanID
        {
            get { return _panID; }
            set { _panID = value; }
        }

        byte _channel;
        /// <summary>
        /// Wireless channel
        /// </summary>
        public byte Channel
        {
            get { return _channel; }
            set { _channel = value; }
        }

        #endregion

        public IlluminatorDevice(ITransport transport, bool runMonitoring = true)
            : base(transport, runMonitoring)
        {
            
        }

        public void PlayCommand(TimeSpan timePoint)
        {
            //System.Diagnostics.Debug.WriteLine("Play: " + timePoint.TotalMilliseconds.ToString());
            WriteCommand(PlaybackState.play, timePoint);
        }

        public void PauseCommand(TimeSpan timePoint)
        {
            //System.Diagnostics.Debug.WriteLine("Pause: " + timePoint.TotalMilliseconds.ToString());
            WriteCommand(PlaybackState.pause, timePoint);
        }

        public void StopCommand(TimeSpan timePoint)
        {
            //System.Diagnostics.Debug.WriteLine("Stop: " + timePoint.TotalMilliseconds.ToString());
            WriteCommand(PlaybackState.stop, timePoint);
        }

        public void SeekCommand(TimeSpan timePoint)
        {
            //System.Diagnostics.Debug.WriteLine("Seek: " + timePoint.TotalMilliseconds.ToString());
            WriteCommand(PlaybackState.seek, timePoint);
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
                    FirmwareVersion = data[2];
                    data.CopyTo(11, Mac, 0, 8);
                    PanID = BitConverter.ToUInt16(data.ToArray(), 19);
                    Channel = data[20];
                }
                catch (Exception exception)
                {
                    if (Logger != null)
                    {
                        Logger.WriteException(exception);
                    }
                    throw exception;
                }
                finally
                {
                    base.Busy = false;
                    base.SetStatus(ConnectionStatus.Online);
                }
            }
        }

        public virtual bool WriteCommand(PlaybackState cmd, TimeSpan timeStamp)
        {
            PixelDevicePacket request = null;
            var parameters = new List<byte>() { (byte)cmd };

            parameters.AddRange(BitConverter.GetBytes((UInt32)timeStamp.TotalMilliseconds));
            request = new PixelDevicePacket(PixelDeviceCommand.PLAYBACK_CONTROL, parameters);
            PixelDevicePacket packet2 = DoSession(request);

            bool result = packet2.Data[0] == 0x0d;
            return result;
        }

    }
}
