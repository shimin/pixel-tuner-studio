using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Common.Helpers;
using Common.ViewModel;
using Communication.Service;

namespace PixelTuner.ViewModel
{
    public class DeviceViewModel : ViewModelBase
    {
        #region Properties

        public SerialDeviceBase Device { get; private set; }

        private Guid aGuid = Guid.NewGuid();

        public bool BootMode => Device?.BootMode ?? false;

        //public bool Busy => Device?.Busy ?? false;
        public bool Busy
        {
            get { return !Device?.Busy ?? true; }
            set { }
        }

        public virtual string FullName
        {
            get;
            protected set;
        }

        public virtual string Name
        {
            get
            {
                return Device == null ? SupportedDevices.TryGetName("") : SupportedDevices.TryGetName(Device.Name);
            }
            protected set { RaisePropertyChanged("Name"); }
        }

        /// <summary>
        /// Версия прошивки
        /// </summary>
        public int FirmwareVersion
        {
            get { return Device?.FirmwareVersion ?? 0; }
            private set { RaisePropertyChanged("FirmwareVersion"); RaisePropertyChanged("FullName"); }
        }

        /// <summary>
        /// Версия загрузчика
        /// </summary>
        public int BootVersion
        {
            get { return Device?.BootVersion ?? 0; }
            private set { RaisePropertyChanged("BootVersion"); RaisePropertyChanged("FullName"); }
        }

        /// <summary>
        /// Версия платформы
        /// </summary>
        public int PlatformVersion
        {
            get { return Device?.PlatformVersion ?? 0; }
            private set { RaisePropertyChanged("PlatformVersion"); RaisePropertyChanged("FullName"); }
        }

        /// <summary>
        /// Свойство определяет, показывать ли контролы, связанные с обновлением прошивки
        /// </summary>
        public virtual bool UpdateNeeded
        {
            get
            {
                return false;
            }
        }
        #endregion

        #region Constructor
        
        public DeviceViewModel()
        {

        }

        public DeviceViewModel(SerialDeviceBase device)
        {
            Device = device;
            Device.PropertyChanged += RaisePropertyChanged;

            device.StatusChanged += device_StatusChanged;
            device.SessionFinished += device_SessionFinished;

            RootViewModel.ApplicationSettingsChanged += ProjectUpdateHandler;
            RootViewModel.This.ActiveDocumentChanged += (x, e) =>
            {
                ProjectUpdateHandler(x, e);
                if (e.OldDocument != null) e.OldDocument.FileChanged -= ProjectUpdateHandler;
                if (e.NewDocument != null) e.NewDocument.FileChanged += ProjectUpdateHandler;
            };
#if DEBUG
            Device.Logger = RootViewModel.Logger;
#endif
        }

        #endregion

        #region Status Info

        public int StatusRaw
        {
            get
            {
                var status = Device == null ? ConnectionStatus.Offline : Device.Status;
                return (int)status;
            }
        }

        public string Status
        {
            get
            {
                var status = Device == null ? ConnectionStatus.Offline : Device.Status;
                return (String)Application.Current.TryFindResource(EnumHelper.GetDescription(status));
            }
        }

        private int _progressMax = 100;
        public int ProgressMax
        {
            get { return _progressMax; }
            set { _progressMax = value; RaisePropertyChanged("ProgressMax"); RaisePropertyChanged("Progress");  }
        }

        int _progressMin;

        public int ProgressMin
        {
            get { return _progressMin; }
            set { _progressMin = value; RaisePropertyChanged("ProgressMin"); RaisePropertyChanged("Progress"); }
        }

        object _sync = new object();

        int _progress;
        public int Progress
        {
            get { return _progress; }
            set
            {
                _progress = value % (_progressMax - _progressMin);
                RaisePropertyChanged("Progress");
            }
        }

        int _step = 1;
        public int Step
        {
            get { return _step; }
            set { RaisePropertyChanged("Step"); RaisePropertyChanged("Progress"); _step = value; }
        }

        #endregion
        
        #region Status handlers

        void ProjectUpdateHandler(object sender, EventArgs e)
        {
            RaisePropertyChanged("ActiveProjectSize");
        }

        void device_SessionFinished(DeviceSessionEventArgs e)
        {
            if (!Busy)
            {
                Progress += Step;
                Console.WriteLine($"Progress: {Progress}");
            }
        }

        void device_StatusChanged(EventArgs e)
        {
            RaisePropertyChanged("StatusRaw");
            RaisePropertyChanged("Status");
        }

        protected void InitializeProgressBar(IList<byte> binaryData)
        {
            ProgressMin = 0;
            ProgressMax = SerialDeviceBase.EstimateRequestsCount(binaryData.Count) + 1;
            Step = 1;
            Console.WriteLine($"Init Params!!!! \n ProgressMin: {ProgressMin}, ProgressMax: {ProgressMax}");
        }

        #endregion

        #region ViewModel commands

        #region UploadFirmwareCommand
        RelayCommand _uploadFirmwareCommand;
        public ICommand UploadFirmwareCommand
        {
            get
            {
                if (_uploadFirmwareCommand == null)
                {
                    _uploadFirmwareCommand = new RelayCommand(p => OnUploadFirmware(p), p => CanUploadFirmware(p));
                }

                return _uploadFirmwareCommand;
            }
        }

        protected virtual bool CanUploadFirmware(object parameter)
        {
            return Device != null && Device.BootMode;
        }

        protected virtual void OnUploadFirmware(object parameter)
        {
            
        }

        #endregion
        
        #region WriteMacCommand
        RelayCommand _writeMacCommand;
        public ICommand WriteMacCommand => _writeMacCommand ?? (_writeMacCommand = new RelayCommand(OnWriteMac, CanWriteMac));

        protected virtual bool CanWriteMac(object parameter)
        {
            var il = RootViewModel.This.DevicesModel.Illuminators.FirstOrDefault();
            return il != null;
        }

        protected virtual void OnWriteMac(object parameter)
        {
            var il = RootViewModel.This.DevicesModel.Illuminators.FirstOrDefault();
            if (il == null) return;
            var mac = il.Device.Mac;
            ushort b1 = (ushort) ((mac[0] << 8 | mac[1]));
            ushort b2 = (ushort)((mac[2] << 8 | mac[3]));
            ushort b3 = (ushort)((mac[4] << 8 | mac[5]));
            ushort b4 = (ushort)((mac[6] << 8 | mac[7]));

            var settings = new List<DeviceFeature>
            {
                new DeviceFeature(6, b1),
                new DeviceFeature(7, b2),
                new DeviceFeature(8, b3),
                new DeviceFeature(9, b4)
            };

            Device.WriteSettings(settings);
        }

        #endregion

        #region UploadCommand
        RelayCommand _uploadCommand;
        public ICommand UploadCommand
        {
            get
            {
                if (_uploadCommand == null)
                {
                    _uploadCommand = new RelayCommand(p => OnUploadCommand(p), p => CanUploadCommand(p));
                }

                return _uploadCommand;
            }
        }

        protected virtual bool CanUploadCommand(object parameter)
        {
            return false;
        }

        protected virtual void OnUploadCommand(object parameter)
        {
            
        }

        #endregion

        #region AutoUpdateCommand
        RelayCommand _autoUpdateCommand;
        public ICommand AutoUpdateCommand
        {
            get
            {
                if (_autoUpdateCommand == null)
                {
                    _autoUpdateCommand = new RelayCommand(p => OnAutoUpdateCommand(p), p => CanAutoUpdateCommand(p));
                }

                return _autoUpdateCommand;
            }
        }

        protected virtual bool CanAutoUpdateCommand(object parameter)
        {
            return (Device != null);
        }

        protected virtual void OnAutoUpdateCommand(object parameter)
        {
            Device.SwitchToBoot();
        }

        #endregion

        #region UpdatePostponeCommand
        RelayCommand _updatePostponeCommand;
        public ICommand UpdatePostponeCommand
        {
            get
            {
                if (_updatePostponeCommand == null)
                {
                    _updatePostponeCommand = new RelayCommand(p => OnUpdatePostponeCommand(p), p => CanUpdatePostponeCommand(p));
                }

                return _updatePostponeCommand;
            }
        }

        protected virtual bool CanUpdatePostponeCommand(object parameter)
        {
            return Device != null;
        }

        protected virtual void OnUpdatePostponeCommand(object parameter)
        {

        }

        #endregion

        #endregion

        #region Misc

        public override string ToString()
        {
            return Name;
        }

        #endregion
    }
}
