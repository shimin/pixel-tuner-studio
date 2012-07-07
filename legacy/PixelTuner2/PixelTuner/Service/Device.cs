using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;

namespace PixelTuner.Service
{
    enum Command
    {
        WHO_IS_IT = 0x53,
        MK_STAT = 0x69,
        VERSION_BOOT = 0x56,
        VERSION_FW = 0x76,
        WRITE_IFLASH_MODE = 0x46,
        WRITE_BLOCK = 0x42,
        END_WRITE_FLASH = 0x43,
    }

    public class Device
    {
        SerialPort _port = null;

        public SerialPort Port
        {
            get { return _port; }
            set { _port = value; }
        }

        public Device(SerialPort port)
        {
            _port = port;
        }

        #region Commands

        public string WhoIsIt()
        {
            byte[] buf = GetCommandBytes(Command.WHO_IS_IT).ToArray();
            _port.Write(buf, 0, buf.Length);

            string result = "";
            
            int ans = _port.ReadByte();
            if (ans == (int)Command.WHO_IS_IT)
            {
                result = _port.ReadExisting();
                //int len = _port.ReadByte();
                //byte[] buffer = new byte[len];
                //int bytesGot = _port.Read(buffer, 0, len);
            }
            return result;
        }

        private List<byte> GetCommandBytes(Command command, List<byte> args = null)
        {
            List<byte> result = new List<byte>() { (byte)command };
            switch (command)
            {
                case Command.WHO_IS_IT: result.Add(0); break;
                default: break;
            }
            result.Add(0x0D);
            return result;
        }

        #endregion

        #region Misc

        public override string ToString()
        {
            return _port != null ? this.Port.PortName : "";
        }

        #endregion
    }
}
