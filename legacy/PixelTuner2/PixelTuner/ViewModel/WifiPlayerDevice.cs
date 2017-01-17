using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Timers;
using Communication.Service;
using PixelTuner.Properties;

namespace PixelTuner.ViewModel
{
    public class WifiPlayerDevice
    {
        UdpClient client = new UdpClient();

        private PlaybackState _currentState = PlaybackState.pause;

        private PlaybackState _previousState = PlaybackState.stop;

        private TimeSpan _currentTime;

        private bool _transmissionAllowed = true;

        public bool IsOnAir { get; set; } = false;

        private Timer _stateUpdater = null;
        
        public WifiPlayerDevice()
        {
            _currentTime = new TimeSpan(0, 0, 0);
            client.EnableBroadcast = true;
            client.DontFragment = true;
            //_stateUpdater = new Timer(500);
            //_stateUpdater.AutoReset = true;
            //_stateUpdater.Elapsed += _stateUpdater_Elapsed;
            //_stateUpdater.Start();
        }

        //private void _stateUpdater_Elapsed(object sender, ElapsedEventArgs e)
        //{
        //    if (IsOnAir)
        //    {
        //        if (_currentState == PlaybackState.stop || _currentState != _previousState)
        //        {
        //            SendCommand(_currentState, _currentTime);
        //            _previousState = _currentState;
        //        }
        //    }
        //}

        public void SendCommand(PlaybackState cmd, TimeSpan timeStamp)
        {
            PixelDevicePacket request = null;
            var parameters = new List<byte>() { (byte)cmd };

            parameters.AddRange(BitConverter.GetBytes((UInt32)timeStamp.TotalMilliseconds));
            request = new PixelDevicePacket(PixelDeviceCommand.PLAYBACK_CONTROL, parameters);
            try
            {
                var ip = new IPEndPoint(IPAddress.Parse(Settings.Default.MulticastIP), 333);
                client.Send(request.RawData.ToArray(), request.RawData.Count, ip);
            }
            catch (Exception ex)
            {
            }
        }

        private void Play(TimeSpan timePoint)
        {
            _currentTime = timePoint;
            _currentState = PlaybackState.play;
            SendCommand(PlaybackState.play, timePoint);
        }

        public void Play(TimeSpan time, int discreteness)
        {
            //_stateUpdater.Stop();
            //_stateUpdater.Start();

            int ms = (int)time.TotalMilliseconds;
            ms = (ms / discreteness) * discreteness;
            var ts = TimeSpan.FromMilliseconds(ms);
            _currentTime = ts;
            Play(ts);
        }

        private void Pause(TimeSpan timePoint)
        {
            _currentTime = timePoint;
            _currentState = PlaybackState.stop;
            SendCommand(PlaybackState.pause, timePoint);
        }

        public void Pause(TimeSpan time, int discreteness)
        {
            //_stateUpdater.Stop();
            //_stateUpdater.Start();

            int ms = (int)time.TotalMilliseconds;
            ms = (ms / discreteness) * discreteness;
            var ts = TimeSpan.FromMilliseconds(ms);
            _currentTime = ts;
            Pause(ts);
        }

        private void Stop(TimeSpan timePoint)
        {
            _currentTime = timePoint;
            _currentState = PlaybackState.stop;
            SendCommand(PlaybackState.stop, timePoint);
        }

        public void Stop(TimeSpan time, int discreteness)
        {
            //_stateUpdater.Stop();
            //_stateUpdater.Start();

            int ms = (int)time.TotalMilliseconds;
            ms = (ms / discreteness) * discreteness;
            var ts = TimeSpan.FromMilliseconds(ms);
            _currentTime = ts;
            Stop(ts);
        }

        private void Seek(TimeSpan timePoint)
        {
            SendCommand(PlaybackState.seek, timePoint);
        }

        public void Seek(TimeSpan time, int discreteness)
        {
            //_stateUpdater.Stop();
            //_stateUpdater.Start();

            int ms = (int)time.TotalMilliseconds;
            ms = (ms / discreteness) * discreteness;
            var ts = TimeSpan.FromMilliseconds(ms);
            _currentTime = ts;
            Seek(ts);
        }
    }
}