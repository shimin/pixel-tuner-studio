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
        /// Доступный максимум памяти
        /// </summary>
        public int MaxMemory
        {
            get { return _maxMemory; }
            set { RaisePropertyChanged("MaxMemory"); _maxMemory = value; }
        }

        int _usedMemory;
        /// <summary>
        /// Использовано памяти
        /// </summary>
        public int UsedMemory
        {
            get { return _usedMemory; }
            set { RaisePropertyChanged("UsedMemory"); _usedMemory = value; }
        }

        int _memoryRate = 0;
        /// <summary>
        /// Отношение занятой памяти к общему объему (1..100)
        /// </summary>
        public int MemoryRate
        {
            get { return _memoryRate; }
            set { RaisePropertyChanged("MemoryRate"); _memoryRate = value; }
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

        string _productID = "";
        /// <summary>
        /// Уникальный идентификатор устройства
        /// </summary>
        public string ProductID
        {
            get { return _productID; }
            set { RaisePropertyChanged("ProductID"); _productID = value; }
        }
    }
}
