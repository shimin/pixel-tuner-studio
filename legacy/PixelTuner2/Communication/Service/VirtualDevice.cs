using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Communication.Service
{
    public class VirtualDevice : VideoDevice
    {
        public VirtualDevice() : base(new VirtualTransport(), false)
        {
            Name = "PLR_v2FW";
            GetInfo();
            SetStatus(ConnectionStatus.Online);
        }
        
        public override void GetInfo()
        {
            List<byte> data = new List<byte>() { 0x02, 0x04, 0x02, 0x00, 0x18, 0x00, 0x00, 0x00, 0x39, 0xFF, 0x98, 0x6C, 0x75, 0x63, 0x6B, 0x79, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x74, 0x65, 0x68, 0x69, 0x62, 0x6F, 0x6F, 0x6D, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0, 0xA8, 0x58, 0xCB, 0x4D, 0x01, 0x73, 0x70, 0x69, 0x6E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            PlatformVersion = data[0];
            BootVersion = data[1];
            FirmwareVersion = data[2];
            LedsTotal = 36;// (ushort)((data[3] << 8) | data[4]);
            EnergyLevel = (data[5] << 8) | data[6];
            byte[] memSizeArray = new byte[4] { data[10], data[9], data[8], data[7] };
            MaxMemory = BitConverter.ToUInt32(memSizeArray, 0);
            Tag = "fan";// Encoding.GetEncoding(1251).GetString(data.ToArray(), 45, 12).TrimEnd(new char[] { (char)0 });
        }

        protected override byte[] DoSession(byte[] query)
        {
            return new byte[query.Length];
        }
    }
}
