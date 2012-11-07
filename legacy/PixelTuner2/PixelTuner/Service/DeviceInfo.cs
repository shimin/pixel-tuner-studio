using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PixelTuner.Service
{
    public class DeviceInfo : ViewModelBase
    {
        int _maxMemory;
        /// <summary>
        /// Доступный максимум памяти в килобайтах
        /// </summary>
        public int MaxMemory
        {
            get { return _maxMemory; }
            set { RaisePropertyChanged("MaxMemory"); _maxMemory = value; }
        }

        float _energyLevel = 0;
        /// <summary>
        /// Уровень заряда аккумулятора (1..100)
        /// </summary>
        public float EnergyLevel
        {
            get { return _energyLevel; }
            set { RaisePropertyChanged("EnergyLevel"); _energyLevel = value; }
        }

        int _firmwareVersion = 0;
        /// <summary>
        /// Версия прошивки
        /// </summary>
        public int FirmwareVersion
        {
            get { return _firmwareVersion; }
            set { RaisePropertyChanged("FirmwareVersion"); _firmwareVersion = value; }
        }

        int _bootVersion = 0;
        /// <summary>
        /// Версия загрузчика
        /// </summary>
        public int BootVersion
        {
            get { return _bootVersion; }
            set { RaisePropertyChanged("BootVersion"); _bootVersion = value; }
        }

        int _platformVersion = 0;
        /// <summary>
        /// Версия платформы
        /// </summary>
        public int PlatformVersion
        {
            get { return _platformVersion; }
            set { RaisePropertyChanged("PlatformVersion"); _platformVersion = value; }
        }

        byte _ledsTotal = 0;
        /// <summary>
        /// Число диодов в устройстве
        /// </summary>
        public byte LedsTotal
        {
            get { return _ledsTotal; }
            set { RaisePropertyChanged("LedsTotal"); _ledsTotal = value; }
        }
    }
}
