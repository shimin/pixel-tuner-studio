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

        private static int FirmwareFileVersion { get; set; }

        private static int FirmwareFileVersionV3 { get; set; }

        private static byte[] _firmwareBytes;

        private static byte[] _firmwareBytesV3;

        private static string _firmwareFileName = "flashLR.bin";

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
            get { return Device == null ? 0 : Device.MemoryAvailable; }
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

        public override bool UpdateNeeded
        {
            get
            {
                if (PlatformVersion == 2)
                    return (FirmwareVersion < FirmwareFileVersion) && !BootMode;
                if (PlatformVersion == 3)
                    return (FirmwareVersion < FirmwareFileVersionV3) && !BootMode;
                return false;
            }
        }

        #endregion

        static LightMasterDeviceViewModel()
        {
            var streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Files/Flash_LR.bin"));
            _firmwareBytes = Common.Collections.StreamHelper.ReadFully(streamInfo.Stream);
            FirmwareFileVersion = _firmwareBytes[52];

            streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Files/Flash_LRv3.bin"));
            _firmwareBytesV3 = Common.Collections.StreamHelper.ReadFully(streamInfo.Stream);
            FirmwareFileVersionV3 = _firmwareBytesV3[52];
        }

        public LightMasterDeviceViewModel(LightMasterDevice device)
            : base(device)
        {
            Device = device;
        }
        
        #region UploadCommand

        protected override bool CanUploadCommand(object parameter)
        {
            return Device != null && !Device.Busy && !BootMode;
        }

        protected override void OnUploadCommand(object parameter)
        {
            Progress = 0;
            FileViewModel model = parameter as FileViewModel;

            Application.Current.MainWindow.Dispatcher.Invoke(new Action(() =>
                {
                    if (model != null)
                    {
                        TimelinesToUpload dialog = new TimelinesToUpload(TimelineFilter, model);
                        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        dialog.Owner = Application.Current.MainWindow;
                        if (dialog.ShowDialog() == true)
                        {
                            List<byte> data = model.GetLightBinaryPresentation(dialog.SelectedTimeLines);
                            if (ValidateProject(model, data.Count/1024))
                            {
                                InitializeProgressBar(data, PixelDevice.UsualPacketSize);
                                BackgroundWorker worker = new BackgroundWorker();
                                worker.DoWork += (x, e) => Device.WriteFile(data, PixelDevice.UsualPacketSize);
                                worker.RunWorkerAsync();
                            }
                        }
                    }
                }
            ));
        }

        public static bool TimelineFilter(TimeLineViewModel l)
        {
            return l.GetType() == typeof(LightTimeLineViewModel);
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
            var fwPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _firmwareFileName);
            return base.CanUploadFirmware(null) && (File.Exists(fwPath) || _firmwareBytes != null);
        }

        /// <summary>
        /// Tries to upload firmware. Firstly, check parameter, than try to load "flashLR.bin" from directory, than load FW from resources
        /// </summary>
        /// <param name="parameter">firmware bytes</param>
        protected override void OnUploadFirmware(object parameter)
        {
            try
            {
                var binaryData = parameter as byte[];

                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _firmwareFileName);
                if (binaryData == null && File.Exists(path))
                    binaryData = File.ReadAllBytes(path);

                if (binaryData == null)
                {
                    switch (PlatformVersion)
                    {
                        case 2:
                            binaryData = _firmwareBytes;
                            break;
                        case 3:
                            binaryData = _firmwareBytesV3;
                            break;
                    }
                }

                if (binaryData == null) return;

                BackgroundWorker worker = new BackgroundWorker();
                InitializeProgressBar(binaryData, PixelDevice.UsualPacketSize);
                worker.DoWork += (x, e) => Device.WriteFirmware(new List<byte>(binaryData));
                worker.RunWorkerAsync();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, Application.Current.MainWindow.Title);
            }
        }

        #endregion

        #region UpdatePostponeCommand

        protected override void OnUpdatePostponeCommand(object parameter)
        {
            FirmwareFileVersion = FirmwareVersion;
            RaisePropertyChanged("UpdateNeeded");
        }

        #endregion

    }
}
