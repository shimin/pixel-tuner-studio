using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PixelTuner.Service
{
    public class PixelDeviceInfo
    {
        string _name;

        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        int _maxMemory;

        public int MaxMemory
        {
            get { return _maxMemory; }
            set { _maxMemory = value; }
        }

        int _usedMemory;

        public int UsedMemory
        {
            get { return _usedMemory; }
            set { _usedMemory = value; }
        }

        float _energyLevel;

        public float EnergyLevel
        {
            get { return _energyLevel; }
            set { _energyLevel = value; }
        }

        string _productID;

        public string ProductID
        {
            get { return _productID; }
            set { _productID = value; }
        }
    }
}
