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
                if (data.Count > Math.Max(PixelDevice.UsualPacketSize, PixelDevice.ProjectPacketSize))
                    throw new ArgumentOutOfRangeException(String.Format("Packet length is over maximum {0}",
                        Math.Max(PixelDevice.UsualPacketSize, PixelDevice.ProjectPacketSize)), "data");
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

        public PixelDevicePacket(byte[] rawData)
        {
            if (rawData.Length < 3) throw new ArgumentException("Binary data size should be grather then 3.");
            RawData = new List<byte>(rawData);
            Data = RawData.GetRange(2, PacketLength);
        }

        private List<byte> GetCommandBytes(PixelDeviceCommand command, IList<byte> args = null)
        {
            List<byte> list = new List<byte> {
                (byte) command
            };
            switch (command)
            {
                case PixelDeviceCommand.WRITE_BLOCK:
                case PixelDeviceCommand.WRITE_SETTINGS:
                    if (args == null)
                    {
                        throw new ArgumentNullException("Dispatching bytes array wasn't specified", "args");
                    }
                    if (args.Count > SerialDeviceBase.UsualPacketSize)
                    {
                        throw new ArgumentOutOfRangeException(string.Format("Packet length is over maximum {0}", SerialDeviceBase.UsualPacketSize), "args");
                    }
                    list.Add((byte)args.Count);
                    list.AddRange(args);
                    break;

                case PixelDeviceCommand.END_WRITE_FLASH:
                    list.Add(0);
                    break;

                case PixelDeviceCommand.WRITE_IFLASH_MODE:
                    list.Add(0);
                    break;

                case PixelDeviceCommand.INFO:
                    list.Add(0);
                    break;

                case PixelDeviceCommand.SWITCH_TO_BOOT:
                    list.Add(0);
                    break;

                case PixelDeviceCommand.END_WRITE_DATA:
                    list.Add(0);
                    break;

                case PixelDeviceCommand.WRITE_IDATA_MODE:
                    list.Add((byte)args.Count);
                    list.AddRange(args);
                    break;

                case PixelDeviceCommand.WHO_IS_IT:
                    list.Add(0);
                    break;
            }
            list.Add(13);
            return list;
        }

    }
}
