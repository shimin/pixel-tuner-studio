using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace PixelTuner
{
    internal class UdpClientBroadcast : UdpClient
    {
        public UdpClientBroadcast()
        {
            try
            {
                Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
            }
            catch (Exception ex)
            {

            }
        }
    }
}
