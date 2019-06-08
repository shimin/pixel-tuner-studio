using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PixelTuner.ViewModel;
using Communication.Service;
using System.Windows.Input;

namespace PixelTuner.ViewModel.Devices
{

    public class WifiLightMasterDeviceViewModel : LightMasterDeviceViewModel
    {
        private string _ip;
        private int _port;
        private RelayCommand _playCommand;
        private RelayCommand _pauseCommand;
        private RelayCommand _stopCommand;

        public WifiLightMasterDeviceViewModel(LightMasterDevice device) : base(device)
        {
            _ip = "";
        }

        private string BytesToString(byte[] src)
        {
            string str = "";
            for (int i = 0; i < src.Length; i++)
            {
                str = str + src[i].ToString("X2") + " ";
            }
            return str;
        }

        private string FormatInfo()
        {
            return $@"Version: {base.PlatformVersion}.{base.BootVersion}.{base.FirmwareVersion} 
Channels: { base.ChannelsAmount } 
Memory: { base.MaxMemory} ";
        }


        public string IP
        {
            get { return _ip; }
            set
            {
                _ip = value;
                RaisePropertyChanged("IP");
                RaisePropertyChanged("ChannelID");
            }
        }

        public int Port
        {
            get { return _port; }
            set
            {
                _port = value;
                RaisePropertyChanged("Port");
                RaisePropertyChanged("ChannelID");
            }
        }

        public string ChannelID =>
            $"{_ip}:{Port}";

        public string LastAnswer
        {
            get { return FormatInfo(); }
            set
            {
            }
        }

        public string Tag => Device.Tag;
        
        public bool CanPause(object parameter) =>
            ((Device != null) && ((Device.Transport != null) && Device.Transport.IsOpen));

        public bool CanPlay(object parameter) =>
            ((Device != null) && ((Device.Transport != null) && Device.Transport.IsOpen));

        public bool CanStop(object parameter) =>
            ((Device != null) && ((Device.Transport != null) && Device.Transport.IsOpen));

        public ICommand PlayCommand
        {
            get
            {
                if (_playCommand == null)
                {
                    _playCommand = new RelayCommand(delegate (object p)
                    {
                        OnPlay(p);
                    }, p => CanPlay(p));
                }
                return _playCommand;
            }
        }

        public ICommand PauseCommand
        {
            get
            {
                if (_pauseCommand == null)
                {
                    _pauseCommand = new RelayCommand(delegate (object p)
                    {
                        OnPause(p);
                    }, p => CanPause(p));
                }
                return _pauseCommand;
            }
        }

        public ICommand StopCommand
        {
            get
            {
                if (_stopCommand == null)
                {
                    _stopCommand = new RelayCommand(delegate (object p)
                    {
                        OnStop(p);
                    }, p => CanStop(p));
                }
                return _stopCommand;
            }
        }

        private void OnPause(object parameter)
        {
            Device.Transport.Write(new byte[] { 80, 5, 3, 0, 0, 0, 0, 13 }, 0, 8);
        }

        private void OnPlay(object parameter)
        {
            Device.Transport.Write(new byte[] { 80, 5, 2, 0, 0, 0, 0, 13 }, 0, 8);
        }

        private void OnStop(object parameter)
        {
            Device.Transport.Write(new byte[] { 80, 5, 4, 0, 0, 0, 0, 13 }, 0, 8);
        }
    }
}