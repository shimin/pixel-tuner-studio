using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.Log;
using System.IO.Ports;

namespace Communication.Service
{
    public class LightMasterDevice : SerialDeviceBase
    {
        public LightMasterDevice(ITransport transport, bool runMonitoring = true)
            : base(transport, runMonitoring)
        {
            WriteReadDelay = 5;
        }

        #region Properties

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

        private string _tag;
        public string Tag
        {
            get { return _tag; }
            set { _tag = value; }
        }

        #endregion

        public override void GetInfo()
        {
            lock (this._longOperation)
            {
                try
                {
                    List<byte> data = base.DoSession(new PixelDevicePacket(PixelDeviceCommand.INFO, null)).Data;
                    int num = (data[4] << 8) | data[5];
                    PlatformVersion = data[0];
                    EnergyLevel = num;
                    BootVersion = data[1];
                    FirmwareVersion = data[2];
                    ChannelsTotal = data[4];
                    BatteryLevel = BitConverter.ToUInt16(data.ToArray(), 4);
                    MemoryAvailable = BitConverter.ToUInt32(data.ToArray(), 6);
                    data.CopyTo(10, Mac, 0, 8);
                    PanID = BitConverter.ToUInt16(data.ToArray(), 18);
                    ChannelID = data[19];
                    data.CopyTo(20, ServerMac, 0, 8);
                    if (PlatformVersion >= 2)
                    {
                        Tag = Encoding.GetEncoding(1251).GetString(data.ToArray(), 45, 12).TrimEnd(new char[] { (char)0 });
                    }
                }
                catch (Exception exception)
                {
                    if (base.Logger != null)
                    {
                        base.Logger.WriteException(exception);
                    }
                    throw exception;
                }
                finally
                {
                    Busy = false;
                    SetStatus(ConnectionStatus.Online);
                }
            }
        }
    }
}

