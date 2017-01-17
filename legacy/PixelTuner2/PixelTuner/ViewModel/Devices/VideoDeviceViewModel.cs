using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Communication.Service;
using PixelTuner.ViewModel.Timelines;

namespace PixelTuner.ViewModel
{
    public class VideoDeviceViewModel : DeviceViewModel
    {
        #region Properties

        public new VideoDevice Device { get; private set; }

        private static int FirmwareFileVersion { get; set; }

        private static byte[] _firmwareBytes;

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
            get { return Device == null ? byte.MinValue : Device.LedsTotal; }
            private set { RaisePropertyChanged("LedsTotal"); }
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
                return ((FirmwareVersion < FirmwareFileVersion) && !BootMode);
            }
        }

        #endregion

        static VideoDeviceViewModel()
        {
            var streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Files/Flash_Video.bin"));
            _firmwareBytes = Common.Collections.StreamHelper.ReadFully(streamInfo.Stream);
            FirmwareFileVersion = _firmwareBytes[52];
        }

        public VideoDeviceViewModel(VideoDevice device)
            : base(device)
        {
            Device = device;
        }

        #region UploadCommand

        protected override bool CanUploadCommand(object parameter)
        {
            return ((Device != null) && !BootMode);
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
                    //var line = dialog.SelectedTimeLines.First();
                    List<byte> data = model.GetVideoBinaryPresentation(dialog.SelectedTimeLines);
                    if (ValidateProject(model, data.Count / 1024))
                    {
                        InitializeProgressBar(data);
                        BackgroundWorker worker = new BackgroundWorker();
                        worker.DoWork += (x, e) => Device.WriteFile(data);
                        worker.RunWorkerAsync();
                    }
                }
            }
        }

        public static bool TimelineFilter(TimeLineViewModel l)
        {
            return l.GetType() == typeof(VideoTimeLineViewModel);
        }

        private bool ValidateProject(FileViewModel file, int projectSize)
        {
            if (false && projectSize >= MaxMemory)
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
                byte[] binaryData = parameter as byte[];

                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _firmwareFileName);
                if (binaryData == null && File.Exists(path))
                    binaryData = File.ReadAllBytes(path);

                if (binaryData == null)
                    binaryData = _firmwareBytes;

                if (binaryData == null) return;

                BackgroundWorker worker = new BackgroundWorker();
                InitializeProgressBar(binaryData);
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
