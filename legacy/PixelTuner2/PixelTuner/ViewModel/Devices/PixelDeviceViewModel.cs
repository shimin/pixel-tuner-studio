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
    public class PixelDeviceViewModel : DeviceViewModel
    {
        #region Properties
        
        public new PixelDevice Device { get; private set; }

        static int FirmwareFileVersionV2 { get; set; }

        static int FirmwareFileVersionV3 { get; set; }

        public int ActiveProjectSize
        {
            get
            {
                return RootViewModel.This.ActiveDocument != null ?
                    RootViewModel.This.ActiveDocument.EvaluateBinarySize(LedsTotal) / 1024 : 0;
            }
        }

        public override string FullName
        {
            get { return String.Format("Pixel Poi {0}.{1}.{2}", PlatformVersion, BootVersion, FirmwareVersion); ; }
            protected set { RaisePropertyChanged("FullName"); }
        }

        /// <summary>
        /// Доступный максимум памяти в байтах
        /// </summary>
        public long MaxMemory
        {
            get { return Device == null ? 0 : Device.MaxMemory; }
            private set { RaisePropertyChanged("MaxMemory"); }
        }

        /// <summary>
        /// Уровень заряда аккумулятора (1..100)
        /// </summary>
        public float EnergyLevel
        {
            get { return Device == null ? 0.0f : Device.EnergyLevel; }
            private set { RaisePropertyChanged("EnergyLevel"); }
        }

        /// <summary>
        /// Число диодов в устройстве
        /// </summary>
        public byte LedsTotal
        {
            get { return Device == null ? byte.MinValue : Device.LedsTotal; }
            private set { RaisePropertyChanged("LedsTotal"); }
        }

        /// <summary>
        /// Свойство определяет, показывать ли контролы, связанные с обновлением прошивки
        /// </summary>
        public override bool UpdateNeeded
        {
            get
            {
                if (PlatformVersion == 2)
                    return (FirmwareVersion < FirmwareFileVersionV2) && !BootMode;
                else if (PlatformVersion == 3)
                    return (FirmwareVersion < FirmwareFileVersionV3) && !BootMode;
                return false;
            }
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
            : base (device)
        {
            Device = device;
        }

        #endregion

        #region ViewModel commands

        #region UploadFirmwareCommand
        
        protected override bool CanUploadFirmware(object parameter)
        {
            return Device != null && Device.BootMode &&
                File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flash.bin"));
        }

        protected override void OnUploadFirmware(object parameter)
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
        
        protected override bool CanUploadCommand(object parameter)
        {
            return Device != null && !BootMode;
        }

        protected override void OnUploadCommand(object parameter)
        {
            Progress = 0;
            FileViewModel file = parameter as FileViewModel;
            if (file == null) return;

            List<byte> data = file.GetBinaryPresentation(LedsTotal);
            InitializeProgressBar(data);
            RaisePropertyChanged("ProgressMax");
            if (ValidateProject(file, data.Count / 1024)) // project size in KB
            {
                BackgroundWorker bgWorker = new BackgroundWorker();
                bgWorker.DoWork += (x, e) => Device.WriteFile(data);
                bgWorker.RunWorkerAsync();
            }
        }

        private bool ValidateProject(FileViewModel file, int projectSize)
        {
            if (projectSize >= MaxMemory)
            {
                MessageBox.Show((String)Application.Current.TryFindResource("Global_NotEnoughtSpace"),
                    Application.Current.MainWindow.Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            if (file.LineHeight > LedsTotal)
            {
                if (MessageBox.Show(
                    String.Format(
                        (String)Application.Current.TryFindResource("Global_TimelineHigherThanDevice"),
                        file.LineHeight,
                        LedsTotal,
                        file.LineHeight - LedsTotal),
                    Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    return false;
                }
            }
            if (file.LineHeight < LedsTotal)
            {
                if (MessageBox.Show(
                    String.Format(
                        (String)Application.Current.TryFindResource("Global_TimelineLowerThanDevice"),
                        file.LineHeight,
                        LedsTotal,
                        LedsTotal - file.LineHeight),
                    Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region AutoUpdateCommand

        protected override bool CanAutoUpdateCommand(object parameter)
        {
            return Device != null;
        }

        protected override void OnAutoUpdateCommand(object parameter)
        {
            Device.SwitchToBoot();
        }
        
        #endregion

        #region UpdatePostponeCommand

        protected override bool CanUpdatePostponeCommand(object parameter)
        {
            return Device != null;
        }

        protected override void OnUpdatePostponeCommand(object parameter)
        {
            if (PlatformVersion == 2) FirmwareFileVersionV2 = FirmwareVersion;
            else if (PlatformVersion == 3) FirmwareFileVersionV3 = FirmwareVersion;
            RaisePropertyChanged("UpdateNeeded");
        }

        #endregion

        #endregion

    }
}
