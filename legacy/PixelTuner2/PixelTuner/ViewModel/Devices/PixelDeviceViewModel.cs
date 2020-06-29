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

        private static int FirmwareFileVersionV2 { get; set; }

        private static int FirmwareFileVersionV3 { get; set; }

        private static int FirmwareFileVersionV4 { get; set; }

        private static int FirmwareFileVersionV5 { get; set; }

        private static int FirmwareFileVersionV6 { get; set; }

        private static byte[] _firmwareBytesV2;

        private static byte[] _firmwareBytesV3;

        private static byte[] _firmwareBytesV4;

        private static byte[] _firmwareBytesV5;

        private static byte[] _firmwareBytesV6;

        private static string _firmwareFileName = "Flash.bin";

        public int ActiveProjectSize
        {
            get
            {
                return RootViewModel.This.ActiveDocument != null ?
                    RootViewModel.This.ActiveDocument.EvaluatePixelBinarySize(LedsTotal) / 1024 : 0;
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
                if (PlatformVersion == 2)
                    return (FirmwareVersion < FirmwareFileVersionV2) && !BootMode;
                if (PlatformVersion == 3)
                    return (FirmwareVersion < FirmwareFileVersionV3) && !BootMode;
                if (PlatformVersion == 4)
                    return (FirmwareVersion < FirmwareFileVersionV4) && !BootMode;
                if (PlatformVersion == 5)
                    return (FirmwareVersion < FirmwareFileVersionV5) && !BootMode;
                if (PlatformVersion == 6)
                    return (FirmwareVersion < FirmwareFileVersionV6) && !BootMode;
                return false;
            }
        }
        #endregion

        #region Constructor

        static PixelDeviceViewModel()
        {
            var streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Files/Flash_v2.bin"));
            _firmwareBytesV2 = Common.Collections.StreamHelper.ReadFully(streamInfo.Stream);
            FirmwareFileVersionV2 = _firmwareBytesV2[52];

            streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Files/Flash_v3.bin"));
            _firmwareBytesV3 = Common.Collections.StreamHelper.ReadFully(streamInfo.Stream);
            FirmwareFileVersionV3 = _firmwareBytesV3[52];

            streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Files/Flash_v4.bin"));
            _firmwareBytesV4 = Common.Collections.StreamHelper.ReadFully(streamInfo.Stream);
            FirmwareFileVersionV4 = _firmwareBytesV4[52];

            streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Files/Flash_v5.bin"));
            _firmwareBytesV5 = Common.Collections.StreamHelper.ReadFully(streamInfo.Stream);
            FirmwareFileVersionV5 = _firmwareBytesV5[52];

            streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Files/Flash_v6.bin"));
            _firmwareBytesV6 = Common.Collections.StreamHelper.ReadFully(streamInfo.Stream);
            FirmwareFileVersionV6 = _firmwareBytesV6[52];
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
            var fwPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _firmwareFileName);
            return base.CanUploadFirmware(null) 
                && (File.Exists(fwPath) 
                   || (_firmwareBytesV2 != null 
                    && _firmwareBytesV3 != null 
                    && _firmwareBytesV4 != null
                    && _firmwareBytesV5 != null
                    && _firmwareBytesV6 != null));
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

                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _firmwareFileName);
                if (binaryData == null)
                    try { binaryData = File.ReadAllBytes(path); }
                    catch { }
                
                if (binaryData == null)
                {
                    if (PlatformVersion == 2) binaryData = _firmwareBytesV2;
                    else if (PlatformVersion == 3) binaryData = _firmwareBytesV3;
                    else if (PlatformVersion == 4) binaryData = _firmwareBytesV4;
                    else if (PlatformVersion == 5) binaryData = _firmwareBytesV5;
                    else if (PlatformVersion == 6) binaryData = _firmwareBytesV6;
                }
                if (binaryData == null) return;

                BackgroundWorker bgWorker = new BackgroundWorker();
                InitializeProgressBar(binaryData, PixelDevice.UsualPacketSize);
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
            return Device != null && !Device.Busy && !BootMode;
        }

        protected override void OnUploadCommand(object parameter)
        {
            //Progress = 0;
            FileViewModel file = parameter as FileViewModel;
            if (file == null) return;

            try
            {
                TimelinesToUpload dialog = new TimelinesToUpload(i => TimelineFilter(i) && (i as PixelTimeLineViewModel).LineHeight == LedsTotal, file);
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dialog.Owner = Application.Current.MainWindow;
                if (dialog.ShowDialog() == true)
                {
                    List<byte> data = file.GetPixelBinaryPresentation(dialog.SelectedTimeLines, LedsTotal);
                    InitializeProgressBar(data, PixelDevice.ProjectPacketSize);

                    bool validationOK = ValidateProject(file, data.Count / 1024);
                    if (validationOK) // project size in KB
                    {
                        BackgroundWorker bgWorker = new BackgroundWorker();
                        bgWorker.DoWork += (x, e) => Device.WriteFile(data, PixelDevice.ProjectPacketSize);
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
            if (projectSize >= MaxMemory * 1024)
            {
                MessageBox.Show((String)Application.Current.TryFindResource("Global_NotEnoughtSpace"),
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
            if (PlatformVersion == 2) FirmwareFileVersionV2 = FirmwareVersion;
            else if (PlatformVersion == 3) FirmwareFileVersionV3 = FirmwareVersion;
            else if (PlatformVersion == 4) FirmwareFileVersionV4 = FirmwareVersion;
            else if (PlatformVersion == 5) FirmwareFileVersionV5 = FirmwareVersion;
            else if (PlatformVersion == 6) FirmwareFileVersionV6 = FirmwareVersion;
            RaisePropertyChanged("UpdateNeeded");
        }

        #endregion

        #region SetAsMasterCommand

        protected override void OnSetAsMaster(object parameter)
        {
            if (Device.PlatformVersion == 4)
            {
                var mac = Device.SelfMac;

                mac = MixBytes2(mac);
                var settings = new List<DeviceFeature>
                    {
                        new DeviceFeature(4, mac),
                    };
                Device.WriteSettings(settings);
                RootViewModel.This.DevicesModel.MasterMac = mac;
            } 
            else if (Device.PlatformVersion == 5 || Device.PlatformVersion == 6)
            {
                var answer = MessageBox.Show("Make device control all others?", "PixelTuner 2", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                List<DeviceFeature> settings = new List<DeviceFeature>();
                if (answer == MessageBoxResult.Yes)
                {
                    var mask = BitConverter.GetBytes((ushort) 0x01);
                    settings.Add(new DeviceFeature(0x0d, mask));
                    Device.WriteSettings(settings);
                }
                else if (answer == MessageBoxResult.No)
                {
                    var mask = BitConverter.GetBytes((ushort) 0x00);
                    settings.Add(new DeviceFeature(0x0d, mask));
                    Device.WriteSettings(settings);
                }
            }
        }

        protected override bool CanSetAsMaster(object parameter)
        {
            return Device.PlatformVersion == 4 || Device.PlatformVersion == 5 || Device.PlatformVersion == 6;
        }

        #endregion

        #endregion
    }
}
