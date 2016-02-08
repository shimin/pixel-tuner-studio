using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Communication.Service
{
    public class LightMasterDeviceInfo : SerialDeviceInfo
    {
        int _maxMemory;
        /// <summary>
        /// Доступный максимум памяти в байтах
        /// </summary>
        public int MaxMemory
        {
            get { return _maxMemory; }
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

        byte _channelsTotal = 0;
        /// <summary>
        /// Число диодов в устройстве
        /// </summary>
        public byte ChannelsTotal
        {
            get { return _channelsTotal; }
            set { _channelsTotal = value; }
        }

        UInt16 _batteryLevel = 0;
        /// <summary>
        /// Уровень заряда аккумулятора в % 0..100
        /// </summary>
        public UInt16 BatteryLevel
        {
            get { return _batteryLevel; }
            set { _batteryLevel = value; }
        }

        UInt32 _memoryAvailable = 0;
        /// <summary>
        /// Доступная память в КБ
        /// </summary>
        public UInt32 MemoryAvailable
        {
            get { return _memoryAvailable; }
            set { _memoryAvailable = value; }
        }

        byte[] _mac = new byte[8];
        /// <summary>
        /// 8 байт MAC-адреса
        /// </summary>
        public byte[] Mac
        {
            get { return _mac; }
            set { _mac = value; }
        }

        UInt16 _panID = 0;
        /// <summary>
        /// Номер сети
        /// </summary>
        public UInt16 PanID
        {
            get { return _panID; }
            set { _panID = value; }
        }

        byte _channelID = 0;
        /// <summary>
        /// Номер канала беспроводной сети
        /// </summary>
        public byte ChannelID
        {
            get { return _channelID; }
            set { _channelID = value; }
        }

        byte[] _serverMac = new byte[8];
        /// <summary>
        /// 8 байт MAC-адрес сервера
        /// </summary>
        public byte[] ServerMac
        {
            get { return _serverMac; }
            set { _serverMac = value; }
        }
    }
}
