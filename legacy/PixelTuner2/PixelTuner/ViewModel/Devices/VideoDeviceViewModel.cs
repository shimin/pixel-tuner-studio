using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Communication.Service;

namespace PixelTuner.ViewModel
{
    public class VideoDeviceViewModel : DeviceViewModel
    {
        #region Properties

        public new VideoDevice Device { get; private set; }

        private static int FirmwareFileVersion { get; set; }

        private static int FirmwareFileVersionV2 { get; set; }

        private static byte[] _firmwareBytes;

        private static byte[] _firmwareBytesV2;

        private static string _firmwareFileName = "flashVideo.bin";

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
        /// Число диодов в устройстве
        /// </summary>
        public ushort LedsTotal
        {
            get { return Device?.LedsTotal ?? byte.MinValue; }
            private set { RaisePropertyChanged("LedsTotal"); }
        }

        /// <summary>
        /// Число диодов в устройстве
        /// </summary>
        public string Tag
        {
            get { return Device == null ? "" : Device.Tag; }
            private set { RaisePropertyChanged("Tag"); }
        }

        /// <summary>
        /// Доступный максимум памяти в байтах
        /// </summary>
        public long MaxMemory
        {
            get { return Device == null ? 0 : Device.MaxMemory; }
            private set { RaisePropertyChanged("MaxMemory"); }
        }
        
        public override bool UpdateNeeded
        {
            get
            {
                if (PlatformVersion == 1)
                    return ((FirmwareVersion < FirmwareFileVersion) && !BootMode);
                if (PlatformVersion == 2)
                    return (FirmwareVersion < FirmwareFileVersionV2) && !BootMode;
                return false;
            }
        }

        #endregion

        static VideoDeviceViewModel()
        {
            var streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Files/Flash_Video.bin"));
            _firmwareBytes = Common.Collections.StreamHelper.ReadFully(streamInfo.Stream);
            FirmwareFileVersion = _firmwareBytes[52];

            var streamInfo2 = Application.GetResourceStream(new Uri("pack://application:,,,/Files/Flash_Video_v2.bin"));
            _firmwareBytesV2 = Common.Collections.StreamHelper.ReadFully(streamInfo2.Stream);
            FirmwareFileVersionV2 = _firmwareBytes[52];
        }

        public VideoDeviceViewModel(VideoDevice device)
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

            if (model != null)
            {
                TimelinesToUpload dialog = new TimelinesToUpload(TimelineFilter, model);
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dialog.Owner = Application.Current.MainWindow;
                if (dialog.ShowDialog() == true && dialog.SelectedTimeLines.Count() > 0)
                {
                    List<byte> data = model.GetVideoBinaryPresentation(dialog.SelectedTimeLines);
                    if (ValidateProject(model, data.Count / 1024))
                    {
                        InitializeProgressBar(data, PixelDevice.ProjectPacketSize);
                        BackgroundWorker worker = new BackgroundWorker();
                        worker.DoWork += (x, e) => Device.WriteFile(data, PixelDevice.ProjectPacketSize);
                        worker.RunWorkerAsync();
                    }
                }
                else
                {
                    TimelinesToUpload d2 = new TimelinesToUpload(TimelineFilterFan, model);
                    d2.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    d2.Owner = Application.Current.MainWindow;
                    if (d2.ShowDialog() == true && d2.SelectedTimeLines.Count() > 0)
                    {
                        List<byte> data = model.GetFanBinaryPresentation(d2.SelectedTimeLines.First());
                        if (ValidateProject(model, data.Count / 1024))
                        {
                            InitializeProgressBar(data, PixelDevice.ProjectPacketSize);
                            BackgroundWorker worker = new BackgroundWorker();
                            worker.DoWork += (x, e) => Device.WriteFile(data, PixelDevice.ProjectPacketSize);
                            worker.RunWorkerAsync();
                        }
                    }
                }
            }
        }

        public static bool TimelineFilter(TimeLineViewModel l)
        {
            return l.GetType() == typeof(VideoTimeLineViewModel);
        }

        public static bool TimelineFilterFan(TimeLineViewModel l)
        {
            return l.GetType() == typeof(PixelTimeLineViewModel);
        }

        private bool ValidateProject(FileViewModel file, int projectSize)
        {
            if (false && projectSize >= MaxMemory * 1024)
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
            return base.CanUploadFirmware(null)
                && (File.Exists(fwPath)
                   || (_firmwareBytes != null
                    && _firmwareBytesV2 != null));
        }

        /// <summary>
        /// Tries to upload firmware. Firstly, check parameter, than try to load "flashLR.bin" from directory, than load FW from resources
        /// </summary>
        /// <param name="parameter">firmware bytes</param>
        protected override void OnUploadFirmware(object parameter)
        {
            try
            {
                byte[] binaryData = parameter as byte[];

                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _firmwareFileName);
                if (binaryData == null && File.Exists(path))
                    try { binaryData = File.ReadAllBytes(path); }
                    catch { }

                if (binaryData == null)
                {
                    if (PlatformVersion == 1) binaryData = _firmwareBytes;
                    else if (PlatformVersion == 2) binaryData = _firmwareBytesV2;
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
            if (PlatformVersion == 1) FirmwareFileVersion = FirmwareVersion;
            else if (PlatformVersion == 2) FirmwareFileVersionV2 = FirmwareVersion;
            RaisePropertyChanged("UpdateNeeded");
        }

        #endregion

    }
}
