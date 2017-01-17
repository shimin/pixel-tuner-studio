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
    public class PixelHoopViewModel : DeviceViewModel
    {
        #region Properties

        public new PixelHoop Device { get; private set; }

        private static int FirmwareFileVersionV1 { get; set; }

        private static byte[] _firmwareBytesV1;

        private static string _firmwareFileName = "Flash.bin";

        public int ActiveProjectSize
        {
            get
            {
                return RootViewModel.This.ActiveDocument != null
                    ? RootViewModel.This.ActiveDocument.EvaluatePixelBinarySize(LedsTotal)/1024
                    : 0;
            }
        }

        public override string FullName
        {
            get
            {
                return String.Format("Pixel Hoop {0}.{1}.{2}", PlatformVersion, BootVersion, FirmwareVersion);
                ;
            }
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
        public ushort LedsTotal
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
                return (FirmwareVersion < FirmwareFileVersionV1) && !BootMode;
            }
        }

        #endregion

        #region Constructor

        static PixelHoopViewModel()
        {
            var streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Files/Flash_Hoop.bin"));
            _firmwareBytesV1 = Common.Collections.StreamHelper.ReadFully(streamInfo.Stream);
            FirmwareFileVersionV1 = _firmwareBytesV1[52];
        }

        public PixelHoopViewModel(PixelHoop device)
            : base(device)
        {
            Device = device;
        }

        #endregion

        #region ViewModel commands

        #region UploadFirmwareCommand

        protected override bool CanUploadFirmware(object parameter)
        {
            var fwPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _firmwareFileName);
            return base.CanUploadFirmware(null) && (File.Exists(fwPath) || _firmwareBytesV1 != null);
        }

        /// <summary>
        /// Tries to upload firmware. Firstly, check parameter, than try to load "Flash.bin" from directory, than load FW from resources
        /// </summary>
        /// <param name="parameter">firmware bytes</param>
        protected override void OnUploadFirmware(object parameter)
        {
            try
            {
                byte[] binaryData = parameter as byte[];

                if (binaryData == null)
                    try
                    {
                        binaryData =
                            File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _firmwareFileName));
                    }
                    catch
                    {
                    }

                if (binaryData == null)
                {
                        binaryData = _firmwareBytesV1;
                }
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
            //Progress = 0;
            FileViewModel file = parameter as FileViewModel;
            if (file == null) return;

            try
            {
                TimelinesToUpload dialog =
                    new TimelinesToUpload(
                        i => TimelineFilter(i) && (i as PixelTimeLineViewModel).LineHeight == LedsTotal, file);
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dialog.Owner = Application.Current.MainWindow;
                if (dialog.ShowDialog() == true)
                {
                    List<byte> data = file.GetPixelBinaryPresentation(dialog.SelectedTimeLines, LedsTotal);
                    InitializeProgressBar(data);

                    bool validationOK = ValidateProject(file, data.Count/1024);
                    if (validationOK) // project size in KB
                    {
                        BackgroundWorker bgWorker = new BackgroundWorker();
                        bgWorker.DoWork += (x, e) => Device.WriteFile(data);
                        bgWorker.RunWorkerAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                if (Device.Logger != null) Device.Logger.WriteException(ex);
            }
        }

        public static bool TimelineFilter(TimeLineViewModel l)
        {
            return l is PixelTimeLineViewModel && l.Items.Count > 0;
        }

        private bool ValidateProject(FileViewModel file, int projectSize)
        {
            if (projectSize >= MaxMemory)
            {
                MessageBox.Show((String) Application.Current.TryFindResource("Global_NotEnoughtSpace"),
                    Application.Current.MainWindow.Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            foreach (var l in file.TimeLines)
            {
                var line = l as PixelTimeLineViewModel;
                if (line == null || !line.IsSelectedToUpload) continue;
                if (line.LineHeight > LedsTotal)
                {
                    if (MessageBox.Show(
                            String.Format(
                                (String) Application.Current.TryFindResource("Global_TimelineHigherThanDevice"),
                                line.LineHeight,
                                LedsTotal,
                                line.LineHeight - LedsTotal),
                            Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) ==
                        MessageBoxResult.No)
                    {
                        return false;
                    }
                }
                if (line.LineHeight < LedsTotal)
                {
                    if (MessageBox.Show(
                            String.Format(
                                (String) Application.Current.TryFindResource("Global_TimelineLowerThanDevice"),
                                line.LineHeight,
                                LedsTotal,
                                LedsTotal - line.LineHeight),
                            Application.Current.MainWindow.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) ==
                        MessageBoxResult.No)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        #endregion

        #region UpdatePostponeCommand

        protected override void OnUpdatePostponeCommand(object parameter)
        {
            FirmwareFileVersionV1 = FirmwareVersion;
            RaisePropertyChanged("UpdateNeeded");
        }

        #endregion

        #endregion
    }
}
