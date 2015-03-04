using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Windows.Input;
using System.Windows;

using Common.ViewModel;
using Communication.Service;

namespace PixelTuner.ViewModel
{
    public class PixelDeviceViewModel : ViewModelBase
    {
        #region Properties
        
        public PixelDevice Device { get; private set; }

        public bool BootMode
        {
            get
            {
                return Device != null ? Device.BootMode : false;
            }
        }

        public bool Busy
        {
            get { return Device == null || Device.Info == null ? false : Device.Busy; }
        }

        public int DeviceVersion
        {
            get 
            {
                int v = 100;
                if (Device != null && Device.Info != null)
                    if (int.TryParse(Name.Substring(10, 1), out v) == true)
                        return v;
                return v; 
            }
        }

        static int FirmwareFileVersionV2 { get; set; }

        static int FirmwareFileVersionV3 { get; set; }

        public int ActiveProjectSize
        {
            get
            {
                return RootViewModel.This.ActiveDocument != null ?
                    RootViewModel.This.ActiveDocument.EvaluateBinarySize(LedsTotal) : 0;
            }
        }

        public string FullName
        {
            get { return String.Format("Pixel Poi {0}.{1}.{2}", this.PlatformVersion, this.BootVersion, this.FirmwareVersion); ; }
            private set { RaisePropertyChanged("FullName"); }
        }

        public string Name
        {
            get 
            {
                return Device == null ? PixelDeviceSpecs.TryGetName("") : PixelDeviceSpecs.TryGetName(Device.Name);
            }
            private set { RaisePropertyChanged("Name"); }
        }

        /// <summary>
        /// Доступный максимум памяти в байтах
        /// </summary>
        public int MaxMemory
        {
            get { return Device == null || Device.Info == null ? 0 : Device.Info.MaxMemory; }
            private set { RaisePropertyChanged("MaxMemory"); }
        }

        /// <summary>
        /// Уровень заряда аккумулятора (1..100)
        /// </summary>
        public float EnergyLevel
        {
            get { return Device == null || Device.Info == null ? 0.0f : Device.Info.EnergyLevel; }
            private set { RaisePropertyChanged("EnergyLevel"); }
        }

        /// <summary>
        /// Число диодов в устройстве
        /// </summary>
        public byte LedsTotal
        {
            get { return Device == null || Device.Info == null ? byte.MinValue : Device.Info.LedsTotal; }
            private set { RaisePropertyChanged("LedsTotal"); }
        }

        /// <summary>
        /// Версия прошивки
        /// </summary>
        public int FirmwareVersion
        {
            get { return Device == null || Device.Info == null ? 0 : Device.Info.FirmwareVersion; }
            private set { RaisePropertyChanged("FirmwareVersion"); RaisePropertyChanged("FullName"); }
        }

        /// <summary>
        /// Версия загрузчика
        /// </summary>
        public int BootVersion
        {
            get { return Device == null || Device.Info == null ? 0 : Device.Info.BootVersion; }
            private set { RaisePropertyChanged("BootVersion"); RaisePropertyChanged("FullName"); }
        }
        
        /// <summary>
        /// Версия платформы
        /// </summary>
        public int PlatformVersion
        {
            get { return Device == null || Device.Info == null ? 0 : Device.Info.PlatformVersion; }
            private set { RaisePropertyChanged("PlatformVersion"); RaisePropertyChanged("FullName"); }
        }

        /// <summary>
        /// Свойство определяет, показывать ли контролы, связанные с обновлением прошивки
        /// </summary>
        public bool UpdateNeeded
        {
            get
            {
                if (DeviceVersion == 2)
                    return (this.FirmwareVersion < FirmwareFileVersionV2) && !this.BootMode;
                else if (DeviceVersion == 3)
                    return (this.FirmwareVersion < FirmwareFileVersionV3) && !this.BootMode;
                return false;
            }
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
        
        #region Constructor

        static PixelDeviceViewModel()
        {
            var streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Files/Flash.bin"));
            var firmwareBytes = Common.Collections.StreamHelper.ReadFully(streamInfo.Stream);
            FirmwareFileVersionV2 = firmwareBytes[52];

            streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Files/Flash_v3.bin"));
            firmwareBytes = Common.Collections.StreamHelper.ReadFully(streamInfo.Stream);
            FirmwareFileVersionV3 = firmwareBytes[52];
        }

        public PixelDeviceViewModel(PixelDevice device)
        {
            Device = device;
            device.StatusChanged += new SerialDeviceBase.StatusDelegate(device_StatusChanged);
            device.SessionFinished += new SerialDeviceBase.SessionDelegate(device_SessionFinished);
            
            RootViewModel.ApplicationSettingsChanged += ProjectUpdateHandler;
            RootViewModel.This.ActiveDocumentChanged += (x, e) => {
                ProjectUpdateHandler(x, e);
                if (e.OldDocument != null) e.OldDocument.FileChanged -= ProjectUpdateHandler;
                if (e.NewDocument != null) e.NewDocument.FileChanged += ProjectUpdateHandler;
            };
#if DEBUG
            Device.Logger = RootViewModel.Logger;
#endif
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

        void ProjectUpdateHandler(object sender, EventArgs e)
        {
            RaisePropertyChanged("ActiveProjectSize");
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

        private bool CanUploadFirmware(object parameter)
        {
            return Device != null && Device.BootMode &&
                File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flash.bin"));
        }

        private void OnUploadFirmware(object parameter)
        {
            try
            {
                byte[] binaryData = parameter as byte[];
                if (binaryData == null)
                    binaryData = File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flash.bin"));
                if (binaryData == null) return;
                BackgroundWorker bgWorker = new BackgroundWorker();
                InitializeProgressBar(binaryData);
                bgWorker.DoWork += (x, e) => Device.WriteFirmware(new List<byte>(binaryData));
                bgWorker.RunWorkerAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Application.Current.MainWindow.Title);
            }
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

        private bool CanUploadCommand(object parameter)
        {
            return Device != null && !this.BootMode;
        }

        private void OnUploadCommand(object parameter)
        {
            Progress = 0;
            FileViewModel file = parameter as FileViewModel;
            if (file == null) return;

            List<byte> data = file.GetBinaryPresentation(this.LedsTotal);
            InitializeProgressBar(data);
            RaisePropertyChanged("ProgressMax");
            if (ValidateProject(file, data.Count))
            {
                BackgroundWorker bgWorker = new BackgroundWorker();
                bgWorker.DoWork += (x, e) => Device.WriteFile(data);
                bgWorker.RunWorkerAsync();
            }
        }

        private bool ValidateProject(FileViewModel file, int projectSize)
        {
            if (projectSize >= this.MaxMemory)
            {
                MessageBox.Show((String)Application.Current.TryFindResource("Global_NotEnoughtSpace"),
                    Application.Current.MainWindow.Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            if (file.LineHeight > this.LedsTotal)
            {
                if (MessageBox.Show(
                    String.Format(
                        (String)Application.Current.TryFindResource("Global_TimelineHigherThanDevice"),
                        file.LineHeight,
                        this.LedsTotal,
                        file.LineHeight - this.LedsTotal),
                    Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    return false;
                }
            }
            if (file.LineHeight < this.LedsTotal)
            {
                if (MessageBox.Show(
                    String.Format(
                        (String)Application.Current.TryFindResource("Global_TimelineLowerThanDevice"),
                        file.LineHeight,
                        this.LedsTotal,
                        this.LedsTotal - file.LineHeight),
                    Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    return false;
                }
            }
            return true;
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

        private bool CanAutoUpdateCommand(object parameter)
        {
            return Device != null;
        }

        private void OnAutoUpdateCommand(object parameter)
        {
            Device.SwitchToBoot();
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

        private bool CanUpdatePostponeCommand(object parameter)
        {
            return Device != null;
        }

        private void OnUpdatePostponeCommand(object parameter)
        {
            if (DeviceVersion == 2) FirmwareFileVersionV2 = this.FirmwareVersion;
            else if (DeviceVersion == 3) FirmwareFileVersionV3 = this.FirmwareVersion;
            RaisePropertyChanged("UpdateNeeded");
        }

        #endregion

        #endregion

        #region Misc
        
        public override string ToString()
        {
            return this.Name;
        }

        public override bool Equals(object obj)
        {
            if (obj is PixelDeviceViewModel)
            {
                if ((obj as PixelDeviceViewModel).Device != null && this.Device != null)
                    this.Device.Equals((obj as PixelDeviceViewModel).Device);
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

        #endregion
    }
}
