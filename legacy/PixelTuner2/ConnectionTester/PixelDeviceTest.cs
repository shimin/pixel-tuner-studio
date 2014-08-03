using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Communication.Service;
using System.IO.Ports;

namespace ConnectionTester
{
    internal class PixelDeviceTest : PixelDevice
    {
        internal PixelDeviceTest(SerialPort port)
            : base(port)
        {

        }

        internal void EnterTestMode()
        {
            List<byte> request = new List<byte>();
            request.Add(0x54); // T
            try
            {
                SendRequest(request);
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex);
            }
        }

        internal void ExitTestMode()
        {
            List<byte> request = new List<byte>();
            request.Add(0x4E); // N
            try
            {
                SendRequest(request);
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex);
            }
        }

        internal IList<byte> SendRequest(List<byte> r)
        {
            return DoSession(r.ToArray());
        }

        internal void SetPixelColor(decimal pixelNum, System.Drawing.Color color)
        {
            List<byte> request = new List<byte>();
            request.Add(0x52);  // [R] [0x07] [no_pix] [R_L][R_H][G_L][G_H][B_L][B_H] 
            request.Add(0x07);
            request.Add((byte)pixelNum);
            request.AddRange(Map8To12(color.R));
            request.AddRange(Map8To12(color.G));
            request.AddRange(Map8To12(color.B));

            try
            {
                SendRequest(request);
            }
            catch (Exception ex)
            {
                Logger.WriteException(ex);
            }
        }

        float Brightness = 100;

        private IList<byte> Map8To12(byte p)
        {
            float colorMult = (float)Brightness / 100;
            p = (byte)(p * colorMult);
            return BitConverter.GetBytes((UInt16)(4095 * p / byte.MaxValue));
        }
    }
}
