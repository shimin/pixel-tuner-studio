using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Communication.Service;
using System.Windows;

namespace PixelTuner.ViewModel
{
    public class EpicPoiDeviceViewModel : DeviceViewModel
    {

        #region Properties

        public new SimplePoiDevice Device { get; private set; }

        static int FirmwareFileVersion { get; set; }

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
                return false;
            }
        }
        #endregion

        public EpicPoiDeviceViewModel()
        {
            var streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Files/SimpleFlash.bin"));
            var firmwareBytes = Common.Collections.StreamHelper.ReadFully(streamInfo.Stream);
            FirmwareFileVersion = firmwareBytes[52];
        }

        public EpicPoiDeviceViewModel(SimplePoiDevice device)
            : base(device)
        {

        }
    }
}
