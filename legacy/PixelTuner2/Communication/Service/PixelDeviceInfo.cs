using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.ViewModel;

namespace Communication.Service
{
    public class PixelDeviceInfo
    {
        long _maxMemory;
        /// <summary>
        /// Доступный максимум памяти в байтах
        /// </summary>
        public long MaxMemory
        {
            get
            {
                if (_maxMemory > 3800000) _maxMemory = 3800000;
                if (_platformVersion == 2) return _maxMemory / 1024;
                else return _maxMemory;
            }
            set { _maxMemory = value; }
        }

        float _energyLevel = 0;
        /// <summary>
        /// Уровень заряда аккумулятора (1..100)
        /// </summary>
        public float EnergyLevel
        {
            get { return _energyLevel; }
            set { _energyLevel = value; }
        }

        int _firmwareVersion = 0;
        /// <summary>
        /// Версия прошивки
        /// </summary>
        public int FirmwareVersion
        {
            get { return _firmwareVersion; }
            set { _firmwareVersion = value; }
        }

        int _bootVersion = 0;
        /// <summary>
        /// Версия загрузчика
        /// </summary>
        public int BootVersion
        {
            get { return _bootVersion; }
            set { _bootVersion = value; }
        }

        int _platformVersion = 0;
        /// <summary>
        /// Версия платформы
        /// </summary>
        public int PlatformVersion
        {
            get { return _platformVersion; }
            set { _platformVersion = value; }
        }

        byte _ledsTotal = 0;
        /// <summary>
        /// Число диодов в устройстве
        /// </summary>
        public byte LedsTotal
        {
            get { return _ledsTotal; }
            set { _ledsTotal = value; }
        }
    }
}
