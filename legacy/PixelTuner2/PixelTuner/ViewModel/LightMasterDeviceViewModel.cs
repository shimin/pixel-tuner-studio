using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Common.ViewModel;
using System.Windows.Input;
using System.Collections.ObjectModel;
using Communication.Service;
using System.Windows;
using Common.Helpers;
using System.ComponentModel;

namespace PixelTuner.ViewModel
{
    public class LightMasterDeviceViewModel : ViewModelBase
    {
        private RelayCommand _autoUpdateCommand = null;
        private int _progress = 0;
        private int _progressMax = 100;
        private int _progressMin = 0;
        private int _step = 1;
        private object _sync = new object();
        private ulong _uid;
        private RelayCommand _updatePostponeCommand = null;
        private RelayCommand _uploadCommand = null;
        private RelayCommand _uploadFirmwareCommand = null;

        public LightMasterDeviceViewModel(LightMasterDevice device)
        {
            Device = device;
            FirmwareVersion = 9;
            Device.StatusChanged += new SerialDeviceBase.StatusDelegate(device_StatusChanged);
            device.SessionFinished += new SerialDeviceBase.SessionDelegate(device_SessionFinished);
        }

        private bool CanAutoUpdateCommand(object parameter)
        {
            return (Device != null);
        }

        private bool CanUpdatePostponeCommand(object parameter)
        {
            return (Device != null);
        }

        private bool CanUploadCommand(object parameter)
        {
            return ((Device != null) && !BootMode);
        }

        private bool CanUploadFirmware(object parameter)
        {
            return (((Device != null) && Device.BootMode) && File.Exists(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flash.bin")));
        }

        private void device_SessionFinished(DeviceSessionEventArgs e)
        {
            Progress += Step;
        }

        private void device_StatusChanged(EventArgs e)
        {
            RaisePropertyChanged("StatusRaw");
            RaisePropertyChanged("Status");
        }

        public override bool Equals(object obj)
        {
            if ((obj is LightMasterDeviceViewModel) && (((obj as LightMasterDeviceViewModel).Device != null) && (Device != null)))
            {
                Device.Equals((obj as LightMasterDeviceViewModel).Device);
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        private void InitializeProgressBar(IList<byte> binaryData)
        {
            ProgressMin = 0;
            ProgressMax = PixelDevice.EstimateRequestsCount(binaryData.Count) + 1;
            Step = 1;
        }

        private void OnAutoUpdateCommand(object parameter)
        {
            Device.SwitchToBoot();
        }

        private void OnUpdatePostponeCommand(object parameter)
        {
            FirmwareFileVersion = FirmwareVersion;
            RaisePropertyChanged("UpdateNeeded");
        }

        private void OnUploadCommand(object parameter)
        {
            DoWorkEventHandler handler = null;
            List<byte> data;
            Progress = 0;
            FileViewModel model = parameter as FileViewModel;
            if (model != null)
            {
                //TimelinesToUpload dialog = new TimelinesToUpload();
                //dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                //dialog.Owner = Application.Current.MainWindow;
                //if (dialog.ShowDialog() == true)
                //{
                    data = model.GetBinaryPresentation(500);
                    InitializeProgressBar(data);
                    RaisePropertyChanged("ProgressMax");
                    BackgroundWorker worker = new BackgroundWorker();
                    if (handler == null)
                    {
                        handler = (x, e) => Device.WriteFile(data);
                    }

                    worker.DoWork += handler;
                    worker.RunWorkerAsync();
                //}
            }
        }

        private void OnUploadFirmware(object parameter)
        {
            try
            {
                DoWorkEventHandler handler = null;
                byte[] binaryData = parameter as byte[];
                if (binaryData == null)
                {
                    binaryData = File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flash.bin"));
                }
                if (binaryData != null)
                {
                    InitializeProgressBar(binaryData);
                    RaisePropertyChanged("ProgressMax");
                    BackgroundWorker worker = new BackgroundWorker();
                    if (handler == null)
                    {
                        handler = (x, e) => Device.WriteFirmware(new List<byte>(binaryData));
                    }
                    worker.DoWork += handler;
                    worker.RunWorkerAsync();
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, Application.Current.MainWindow.Title);
            }
        }

        public override string ToString()
        {
            return Name;
        }

        private bool ValidateProject(FileViewModel file, int projectSize)
        {
            if (projectSize >= MaxMemory)
            {
                MessageBox.Show((string)Application.Current.TryFindResource("Global_NotEnoughtSpace"), Application.Current.MainWindow.Title, MessageBoxButton.OK, MessageBoxImage.Asterisk);
                return false;
            }
            if ((file.LineHeight > ChannelsAmount) && (MessageBox.Show(string.Format((string)Application.Current.TryFindResource("Global_TimelineHigherThanDevice"), file.LineHeight, ChannelsAmount, file.LineHeight - ChannelsAmount), Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No))
            {
                return false;
            }
            if ((file.LineHeight < ChannelsAmount) && (MessageBox.Show(string.Format((string)Application.Current.TryFindResource("Global_TimelineLowerThanDevice"), file.LineHeight, ChannelsAmount, ChannelsAmount - file.LineHeight), Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No))
            {
                return false;
            }
            return true;
        }

        private byte _address;
        public byte Address
        {
            get
            {
                return _address;
            }
        }

        public ICommand AutoUpdateCommand
        {
            get
            {
                Action<object> execute = null;
                Predicate<object> canExecute = null;
                if (_autoUpdateCommand == null)
                {
                    if (execute == null)
                    {
                        execute = delegate(object p)
                        {
                            OnAutoUpdateCommand(p);
                        };
                    }
                    if (canExecute == null)
                    {
                        canExecute = p => CanAutoUpdateCommand(p);
                    }
                    _autoUpdateCommand = new RelayCommand(execute, canExecute);
                }
                return _autoUpdateCommand;
            }
        }

        public bool BootMode
        {
            get
            {
                return ((Device != null) ? Device.BootMode : false);
            }
        }

        public int BootVersion
        {
            get
            {
                return Device == null ? 0 : Device.BootVersion;
            }
            private set
            {
                RaisePropertyChanged("BootVersion");
                RaisePropertyChanged("FullName");
            }
        }

        public bool Busy
        {
            get
            {
                return Device != null && Device.Busy;
            }
        }

        public LightMasterDevice Device { get; private set; }

        private int FirmwareFileVersion { get; set; }

        public int FirmwareVersion
        {
            get
            {
                return Device == null ? 0 : Device.FirmwareVersion;
            }
            private set
            {
                RaisePropertyChanged("FirmwareVersion");
                RaisePropertyChanged("FullName");
            }
        }

        public string FullName
        {
            get
            {
                return ((Device == null) ? SupportedDevices.TryGetName("") : string.Format("{0} {1}.{2}.{3}", 
                    new object[] { SupportedDevices.TryGetName(Device.Name), PlatformVersion, BootVersion, FirmwareVersion }));
            }
            private set
            {
                RaisePropertyChanged("FullName");
            }
        }

        public byte ChannelsAmount
        {
            get
            {
                return Device == null ? byte.MinValue : Device.ChannelsTotal;
            }
            private set
            {
                RaisePropertyChanged("LedsTotal");
            }
        }

        public int MaxMemory
        {
            get
            {
                return Device == null ? 0 : Device.MaxMemory;
            }
            private set
            {
                RaisePropertyChanged("MaxMemory");
            }
        }

        public string Name
        {
            get
            {
                return Device == null ? SupportedDevices.TryGetName("") : SupportedDevices.TryGetName(Device.Name);
            }
            private set
            {
                RaisePropertyChanged("Name");
            }
        }

        public int PlatformVersion
        {
            get
            {
                return Device == null ? 0 : Device.PlatformVersion;
            }
            private set
            {
                RaisePropertyChanged("PlatformVersion");
                RaisePropertyChanged("FullName");
            }
        }

        public int Progress
        {
            get
            {
                return _progress;
            }
            set
            {
                _progress = value % (_progressMax - _progressMin);
                RaisePropertyChanged("Progress");
            }
        }

        public int ProgressMax
        {
            get
            {
                return _progressMax;
            }
            set
            {
                RaisePropertyChanged("ProgressMax");
                RaisePropertyChanged("Progress");
                _progressMax = value;
            }
        }

        public int ProgressMin
        {
            get
            {
                return _progressMin;
            }
            set
            {
                RaisePropertyChanged("ProgressMin");
                RaisePropertyChanged("Progress");
                _progressMin = value;
            }
        }

        public string Status
        {
            get
            {
                ConnectionStatus status = (Device == null) ? ConnectionStatus.Offline : Device.Status;
                return (string)Application.Current.TryFindResource(EnumHelper.GetDescription(status));
            }
        }

        public int StatusRaw
        {
            get
            {
                ConnectionStatus status = (Device == null) ? ConnectionStatus.Offline : Device.Status;
                return (int)status;
            }
        }

        public int Step
        {
            get
            {
                return _step;
            }
            set
            {
                RaisePropertyChanged("Step");
                RaisePropertyChanged("Progress");
                _step = value;
            }
        }

        private byte _subAddress;
        public byte SubAddress
        {
            get
            {
                return _subAddress;
            }
        }

        public ReadOnlyObservableCollection<FileViewModel> SuitableFiles
        {
            get
            {
                return RootViewModel.This.Files;
            }
            set
            {
            }
        }

        public ulong Uid
        {
            get
            {
                return _uid;
            }
            set
            {
                _uid = value;
            }
        }

        public bool UpdateNeeded
        {
            get
            {
                return ((FirmwareVersion < FirmwareFileVersion) && !BootMode);
            }
        }

        public ICommand UpdatePostponeCommand
        {
            get
            {
                Action<object> execute = null;
                Predicate<object> canExecute = null;
                if (_updatePostponeCommand == null)
                {
                    if (execute == null)
                    {
                        execute = delegate(object p)
                        {
                            OnUpdatePostponeCommand(p);
                        };
                    }
                    if (canExecute == null)
                    {
                        canExecute = p => CanUpdatePostponeCommand(p);
                    }
                    _updatePostponeCommand = new RelayCommand(execute, canExecute);
                }
                return _updatePostponeCommand;
            }
        }

        public ICommand UploadCommand
        {
            get
            {
                Action<object> execute = null;
                Predicate<object> canExecute = null;
                if (_uploadCommand == null)
                {
                    if (execute == null)
                    {
                        execute = delegate(object p)
                        {
                            OnUploadCommand(p);
                        };
                    }
                    if (canExecute == null)
                    {
                        canExecute = p => CanUploadCommand(p);
                    }
                    _uploadCommand = new RelayCommand(execute, canExecute);
                }
                return _uploadCommand;
            }
        }

        public ICommand UploadFirmwareCommand
        {
            get
            {
                Action<object> execute = null;
                Predicate<object> canExecute = null;
                if (_uploadFirmwareCommand == null)
                {
                    if (execute == null)
                    {
                        execute = delegate(object p)
                        {
                            OnUploadFirmware(p);
                        };
                    }
                    if (canExecute == null)
                    {
                        canExecute = p => CanUploadFirmware(p);
                    }
                    _uploadFirmwareCommand = new RelayCommand(execute, canExecute);
                }
                return _uploadFirmwareCommand;
            }
        }
    }
}
