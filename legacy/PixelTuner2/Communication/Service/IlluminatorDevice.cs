using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;

namespace Communication.Service
{
    public class IlluminatorDevice : SerialDeviceBase
    {
        public enum PlaybackCommand
        {
            play = 2,
            pause = 3,
            stop = 4,
            seek = 5
        }

        public IlluminatorDevice(SerialPort port)
            : base(port)
        {
            
        }

        public void PlayCommand(TimeSpan timePoint)
        {
            //System.Diagnostics.Debug.WriteLine("Play: " + timePoint.TotalMilliseconds.ToString());
            WriteCommand(PlaybackCommand.play, timePoint);
        }

        public void PauseCommand(TimeSpan timePoint)
        {
            //System.Diagnostics.Debug.WriteLine("Pause: " + timePoint.TotalMilliseconds.ToString());
            WriteCommand(PlaybackCommand.pause, timePoint);
        }

        public void StopCommand(TimeSpan timePoint)
        {
            //System.Diagnostics.Debug.WriteLine("Stop: " + timePoint.TotalMilliseconds.ToString());
            WriteCommand(PlaybackCommand.stop, timePoint);
        }

        public void SeekCommand(TimeSpan timePoint)
        {
            //System.Diagnostics.Debug.WriteLine("Seek: " + timePoint.TotalMilliseconds.ToString());
            WriteCommand(PlaybackCommand.seek, timePoint);
        }

        public override SerialDeviceInfo GetInfo()
        {
            lock (this._longOperation)
            {
                base.Busy = true;
                base.SetStatus(ConnectionStatus.Downloading);
                IlluminatorDeviceInfo info = new IlluminatorDeviceInfo();
                try
                {
                    List<byte> data = base.DoSession(new PixelDevicePacket(PixelDeviceCommand.INFO, null)).Data;
                    info.PlatformVersion = data[0];
                    info.BootVersion = data[1];
                    info.FirmwareVersion = data[2];
                    data.CopyTo(3, info.Mac, 0, 8);
                    info.PanID = BitConverter.ToUInt16(data.ToArray(), 11);
                    info.Channel = data[13];
                }
                catch (Exception exception)
                {
                    if (Logger != null)
                    {
                        Logger.WriteException(exception);
                    }
                }
                finally
                {
                    base.Busy = false;
                    base.SetStatus(ConnectionStatus.Online);
                }
                return info;
            }
        }

        public virtual bool WriteCommand(PlaybackCommand cmd, TimeSpan timeStamp)
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
