using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Media;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Input;
using Communication.Binarize;
using NAudio;
using PixelTuner.Annotations;
using PixelTuner.Properties;

namespace PixelTuner.ViewModel.Devices
{
    public class WifiInfoDevice : INotifyPropertyChanged
    {
        UdpClient _client = new UdpClient();

        public WifiInfoDevice()
            : this(Settings.Default.MulticastIP, Settings.Default.WifiPort)
        {
        }

        public WifiInfoDevice(string ip, int port)
        {
            _client.DontFragment = true;
            _client.Connect(ip, port);
            _iPString = ip;
        }

        string _iPString;
        
        public string IPString
        {
            get { return _iPString; }
            set { _iPString = value; OnPropertyChanged("IPString"); }
        }

        private string _lastAnswer;

        public string LastAnswer
        {
            get { return _lastAnswer; }
            set { _lastAnswer = value; OnPropertyChanged("LastAnswer"); }
        }

        #region PlayAudioCommand
        RelayCommand _playCommand = null;
        public ICommand PlayCommand
        {
            get
            {
                if (_playCommand == null)
                {
                    _playCommand = new RelayCommand((p) => OnPlay(p), (p) => CanPlay(p));
                }

                return _playCommand;
            }
        }

        private bool CanPlay(object parameter)
        {
            return true;
        }

        private void OnPlay(object parameter)
        {
            if (_client != null)
            {
                var cmd = new byte[] { 0x50, 0x05, 0x02, 0x00, 0x00, 0x00, 0x00, 0x0d };
                _client.Send(cmd, 8);
            }
        }
        
        #endregion

        #region PauseCommand
        RelayCommand _pauseCommand = null;
        public ICommand PauseCommand
        {
            get
            {
                if (_pauseCommand == null)
                {
                    _pauseCommand = new RelayCommand((p) => OnPause(p), (p) => CanPause(p));
                }

                return _pauseCommand;
            }
        }

        private bool CanPause(object parameter)
        {
            return true;
        }

        private void OnPause(object parameter)
        {
                if (_client != null)
                {
                    var cmd = new byte[] { 0x50, 0x05, 0x03, 0x00, 0x00, 0x00, 0x00, 0x0d };
                    _client.Send(cmd, 8);
                }
        }

        #endregion

        #region StopAudioCommand
        RelayCommand _stopCommand = null;
        public ICommand StopCommand
        {
            get
            {
                if (_stopCommand == null)
                {
                    _stopCommand = new RelayCommand((p) => OnStop(p), (p) => CanStop(p));
                }

                return _stopCommand;
            }
        }

        private bool CanStop(object parameter)
        {
            return true;
        }

        private void OnStop(object parameter)
        {
            if (_client != null)
            {
                _client.Send(new byte[] { 0x50, 0x05, 0x04, 0x00, 0x00, 0x00, 0x00, 0x0d }, 8);
            }
        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Init()
        {
            //_client.ExclusiveAddressUse = false;
            
        }
    }
}
