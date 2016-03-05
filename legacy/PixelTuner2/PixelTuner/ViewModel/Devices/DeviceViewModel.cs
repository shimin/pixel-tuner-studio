using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.ViewModel;
using Communication.Service;
using System.Windows;
using System.Windows.Input;

namespace PixelTuner.ViewModel
{
    public class DeviceViewModel : ViewModelBase
    {
        #region Properties

        public SerialDeviceBase Device { get; private set; }

        public bool BootMode
        {
            get
            {
                return Device != null ? Device.BootMode : false;
            }
        }

        public bool Busy
        {
            get { return Device == null ? false : Device.Busy; }
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
            get { return Device == null ? 0 : Device.FirmwareVersion; }
            private set { RaisePropertyChanged("FirmwareVersion"); RaisePropertyChanged("FullName"); }
        }

        /// <summary>
        /// Версия загрузчика
        /// </summary>
        public int BootVersion
        {
            get { return Device == null ? 0 : Device.BootVersion; }
            private set { RaisePropertyChanged("BootVersion"); RaisePropertyChanged("FullName"); }
        }

        /// <summary>
        /// Версия платформы
        /// </summary>
        public int PlatformVersion
        {
            get { return Device == null ? 0 : Device.PlatformVersion; }
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

            device.StatusChanged += new SerialDeviceBase.StatusDelegate(device_StatusChanged);
            device.SessionFinished += new SerialDeviceBase.SessionDelegate(device_SessionFinished);

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
                return (String)Application.Current.TryFindResource(Common.Helpers.EnumHelper.GetDescription(status));
            }
        }

        private int _progressMax = 100;
        public int ProgressMax
        {
            get { return _progressMax; }
            set { RaisePropertyChanged("ProgressMax"); RaisePropertyChanged("Progress"); _progressMax = value; }
        }

        int _progressMin = 0;

        public int ProgressMin
        {
            get { return _progressMin; }
            set { RaisePropertyChanged("ProgressMin"); RaisePropertyChanged("Progress"); _progressMin = value; }
        }

        object _sync = new object();

        int _progress = 0;
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
            Progress += Step;
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
        }

        #endregion

        #region ViewModel commands

        #region UploadFirmwareCommand
        RelayCommand _uploadFirmwareCommand = null;
        public ICommand UploadFirmwareCommand
        {
            get
            {
                if (_uploadFirmwareCommand == null)
                {
                    _uploadFirmwareCommand = new RelayCommand((p) => OnUploadFirmware(p), (p) => CanUploadFirmware(p));
                }

                return _uploadFirmwareCommand;
            }
        }

        protected virtual bool CanUploadFirmware(object parameter)
        {
            return false;
        }

        protected virtual void OnUploadFirmware(object parameter)
        {
            
        }

        #endregion

        #region UploadCommand
        RelayCommand _uploadCommand = null;
        public ICommand UploadCommand
        {
            get
            {
                if (_uploadCommand == null)
                {
                    _uploadCommand = new RelayCommand((p) => OnUploadCommand(p), (p) => CanUploadCommand(p));
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
        RelayCommand _autoUpdateCommand = null;
        public ICommand AutoUpdateCommand
        {
            get
            {
                if (_autoUpdateCommand == null)
                {
                    _autoUpdateCommand = new RelayCommand((p) => OnAutoUpdateCommand(p), (p) => CanAutoUpdateCommand(p));
                }

                return _autoUpdateCommand;
            }
        }

        protected virtual bool CanAutoUpdateCommand(object parameter)
        {
            return false;
        }

        protected virtual void OnAutoUpdateCommand(object parameter)
        {
            
        }

        #endregion

        #region UpdatePostponeCommand
        RelayCommand _updatePostponeCommand = null;
        public ICommand UpdatePostponeCommand
        {
            get
            {
                if (_updatePostponeCommand == null)
                {
                    _updatePostponeCommand = new RelayCommand((p) => OnUpdatePostponeCommand(p), (p) => CanUpdatePostponeCommand(p));
                }

                return _updatePostponeCommand;
            }
        }

        protected virtual bool CanUpdatePostponeCommand(object parameter)
        {
            return false;
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
