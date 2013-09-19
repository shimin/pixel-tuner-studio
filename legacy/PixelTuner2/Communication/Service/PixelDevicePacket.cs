using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Communication.Service
{
    public class PixelDevicePacket
    {
        public List<byte> RawData { get; private set; }

        public List<byte> Data { get; private set; }

        public byte PacketID
        {
            get { return RawData[0]; }
        }

        public byte PacketLength
        {
            get { return RawData[1]; }
        }

        public PixelDevicePacket(PixelDeviceCommand command, IList<byte> data = null)
        {
            RawData = new List<byte>();
            RawData.Add((byte)command);
            if (command == PixelDeviceCommand.WRITE_BLOCK)
            {
                if (data == null)
                    throw new ArgumentNullException("Dispatching bytes array wasn't specified", "data");
                if (data.Count > PixelDevice.UsualPacketSize)
                    throw new ArgumentOutOfRangeException(String.Format("Packet length is over maximum {0}", PixelDevice.UsualPacketSize), "data");
            }
            if (data != null)
            {
                RawData.Add((byte)data.Count);
                RawData.AddRange(data);
            }
            else RawData.Add(0);
            RawData.Add(0x0d);
        }

        public PixelDevicePacket(List<byte> rawData)
        {
            if (rawData.Count < 3) throw new ArgumentException("Binary data size should be grather then 3.");
            RawData = rawData;
            Data = rawData.GetRange(2, PacketLength);
        }
    }
}
