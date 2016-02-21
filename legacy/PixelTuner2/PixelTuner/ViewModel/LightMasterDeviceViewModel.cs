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
    public class LightMasterDeviceViewModel : DeviceViewModel
    {
        #region Properties

        public new LightMasterDevice Device { get; private set; }

        private int FirmwareFileVersion { get; set; }

        public override string FullName
        {
            get
            {
                return ((Device == null) ? SupportedDevices.TryGetName("") : string.Format("{0} {1}.{2}.{3}",
                    new object[] { SupportedDevices.TryGetName(Device.Name), PlatformVersion, BootVersion, FirmwareVersion }));
            }
            protected set
            {
                RaisePropertyChanged("FullName");
            }
        }

        /// <summary>
        /// Доступный максимум памяти в байтах
        /// </summary>
        public long MaxMemory
        {
            get { return Device == null ? 0 : Device.MaxMemory; }
            private set { RaisePropertyChanged("MaxMemory"); }
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

        public bool UpdateNeeded
        {
            get
            {
                return ((FirmwareVersion < FirmwareFileVersion) && !BootMode);
            }
        }

        #endregion

        public LightMasterDeviceViewModel()
        {
            var streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Files/Flash_LR.bin"));
            var firmwareBytes = Common.Collections.StreamHelper.ReadFully(streamInfo.Stream);
            FirmwareFileVersion = firmwareBytes[52];
        }

        public LightMasterDeviceViewModel(LightMasterDevice device)
            : base(device)
        {
            Device = device;
            //FirmwareVersion = 9;
        }
        
        #region UploadCommand

        protected override bool CanUploadCommand(object parameter)
        {
            return ((Device != null) && !BootMode);
        }

        protected override void OnUploadCommand(object parameter)
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
                if (ValidateProject(model, data.Count / 1024))
                {
                    InitializeProgressBar(data);
                    RaisePropertyChanged("ProgressMax");
                    BackgroundWorker worker = new BackgroundWorker();
                    if (handler == null)
                    {
                        handler = (x, e) => Device.WriteFile(data);
                    }

                    worker.DoWork += handler;
                    worker.RunWorkerAsync();
                }
                //}
            }
        }

        private bool ValidateProject(FileViewModel file, int projectSize)
        {
            if (projectSize >= MaxMemory)
            {
                MessageBox.Show((string)Application.Current.TryFindResource("Global_NotEnoughtSpace"),
                    Application.Current.MainWindow.Title, MessageBoxButton.OK, MessageBoxImage.Asterisk);
                return false;
            }
            return true;
        }

        #endregion

        #region UploadFirmwareCommand

        protected override bool CanUploadFirmware(object parameter)
        {
            return (((Device != null) && Device.BootMode) && File.Exists(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flash.bin")));
        }

        protected override void OnUploadFirmware(object parameter)
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

        #endregion

        #region AutoUpdateCommand

        protected override bool CanAutoUpdateCommand(object parameter)
        {
            return (Device != null);
        }

        protected override void OnAutoUpdateCommand(object parameter)
        {
            Device.SwitchToBoot();
        }

        #endregion        

        #region UpdatePostponeCommand

        protected override bool CanUpdatePostponeCommand(object parameter)
        {
            return (Device != null);
        }

        protected override void OnUpdatePostponeCommand(object parameter)
        {
            FirmwareFileVersion = FirmwareVersion;
            RaisePropertyChanged("UpdateNeeded");
        }

        #endregion

    }
}
