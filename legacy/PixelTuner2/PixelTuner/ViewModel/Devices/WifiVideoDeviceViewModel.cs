using Communication.Service;
using PixelTuner.ViewModel;
using System;
using System.Windows.Input;

namespace PixelTuner.ViewModel.Devices
{
    public class WifiVideoDeviceViewModel : VideoDeviceViewModel
    {
        private string _ip;
        private int _port;
        private RelayCommand _playCommand;
        private RelayCommand _pauseCommand;
        private RelayCommand _stopCommand;
        private System.Timers.Timer _infoUpdate;

        public WifiVideoDeviceViewModel(VideoDevice device) : base(device)
        {
            this._ip = "";
            _infoUpdate = new System.Timers.Timer(5000);
            _infoUpdate.AutoReset = true;
            _infoUpdate.Elapsed += (s, e) => { RaisePropertyChanged("LastAnswer"); };
            _infoUpdate.Start();
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
Leds: { base.LedsTotal }
Memory: { base.MaxMemory } Mb
Charged: { base.EnergyLevel }%
Tag: {base.Tag}";
        }

        public string IP
        {
            get { return this._ip; }
            set
            {
                this._ip = value;
                this.RaisePropertyChanged("IP");
                this.RaisePropertyChanged("ChannelID");
            }
        }

        public int Port
        {
            get { return this._port; }
            set
            {
                this._port = value;
                this.RaisePropertyChanged("Port");
                this.RaisePropertyChanged("ChannelID");
            }
        }

        public string ChannelID => $"{this._ip}:{this.Port}";

        public string LastAnswer
        {
            get { return FormatInfo(); }
            set { }
        }

        public bool CanPause(object parameter) => ((Device != null) && ((Device.Transport != null) && Device.Transport.IsOpen));

        public bool CanPlay(object parameter) => ((Device != null) && ((Device.Transport != null) && Device.Transport.IsOpen));

        public bool CanStop(object parameter) => ((Device != null) && ((Device.Transport != null) && Device.Transport.IsOpen));

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

        protected override bool CanUploadCommand(object parameter)
        {
            return base.CanUploadCommand(parameter) && RootViewModel.This.ActiveDocument != null;
        }

        protected override void OnUploadCommand(object parameter)
        {
            base.OnUploadCommand(RootViewModel.This.ActiveDocument);
        }

    }
}
