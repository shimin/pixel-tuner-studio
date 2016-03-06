using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Communication.Service;
using System.Windows;
using System.ComponentModel;
using System.IO;

namespace PixelTuner.ViewModel
{
    public class EpicPoiDeviceViewModel : DeviceViewModel
    {

        #region Properties

        public new SimplePoiDevice Device { get; private set; }

        private static int FirmwareFileVersion { get; set; }

        private static byte[] _firmwareBytes;

        private static string _firmwareFileName = "epicflash.bin";

        public int ActiveProjectSize
        {
            get
            {
                return RootViewModel.This.ActiveDocument != null ?
                    EvaluateBinarySize(RootViewModel.This.ActiveDocument) / 1024 : 0;
            }
        }

        private int EvaluateBinarySize(FileViewModel fileViewModel)
        {
            //fileViewModel.TimeLines.SelectMany<SimpleTimelineViewModel>
            return 100;
        }

        public override string FullName
        {
            get { return String.Format("Simple Poi {0}.{1}.{2}", PlatformVersion, BootVersion, FirmwareVersion); ; }
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
        /// Свойство определяет, показывать ли контролы, связанные с обновлением прошивки
        /// </summary>
        public override bool UpdateNeeded
        {
            get
            {
                return (FirmwareVersion < FirmwareFileVersion) && !BootMode;
            }
        }
        #endregion

        static EpicPoiDeviceViewModel()
        {
            var streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Files/Flash_Epic.bin"));
            _firmwareBytes = Common.Collections.StreamHelper.ReadFully(streamInfo.Stream);
            FirmwareFileVersion = _firmwareBytes[52];
        }

        public EpicPoiDeviceViewModel(SimplePoiDevice device)
            : base(device)
        {

        }

        #region UploadFirmwareCommand

        protected override bool CanUploadFirmware(object parameter)
        {
            var fwPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _firmwareFileName);            
            return base.CanUploadFirmware(null) && (File.Exists(fwPath) || _firmwareBytes != null);
        }

        /// <summary>
        /// Tries to upload firmware. Firstly, check parameter, than try to load "epicFlash.bin" from directory, than load FW from resources
        /// </summary>
        /// <param name="parameter">firmware bytes</param>
        protected override void OnUploadFirmware(object parameter)
        {
            byte[] binaryData = parameter as byte[];
            
            if (binaryData == null)
                binaryData = File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _firmwareFileName));
            
            if (binaryData == null)
                binaryData = _firmwareBytes;

            if (binaryData == null) return;

            BackgroundWorker bgWorker = new BackgroundWorker();
            InitializeProgressBar(binaryData);
            bgWorker.DoWork += (x, e) => Device.WriteFirmware(new List<byte>(binaryData));
            bgWorker.RunWorkerAsync();
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

            TimelinesToUpload dialog = new TimelinesToUpload(TimelineFilter);
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialog.Owner = Application.Current.MainWindow;
            if (dialog.ShowDialog() == true)
            {
                List<byte> data = file.GetEpicBinaryPresentation(dialog.SelectedTimeLines);
                InitializeProgressBar(data);
                RaisePropertyChanged("ProgressMax");
                if (data.Count / 1024 <= MaxMemory) // project size in KB
                {
                    BackgroundWorker bgWorker = new BackgroundWorker();
                    bgWorker.DoWork += (x, e) => Device.WriteFile(data);
                    bgWorker.RunWorkerAsync();
                }
                else
                {
                    MessageBox.Show((String)Application.Current.TryFindResource("Global_NotEnoughtSpace"),
                        Application.Current.MainWindow.Title, MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private bool TimelineFilter(TimeLineViewModel l)
        {
            return l is EpicPoiTimeLineViewModel && l.Items.Count > 0;
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
