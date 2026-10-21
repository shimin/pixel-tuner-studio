using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace EpicPlayer.ViewModel
{
    // State object for receiving data from remote device.
    public class StateObject
    {
        // Client socket.
        public Socket workSocket = null;
        // Size of receive buffer.
        public const int BufferSize = 256;
        // Receive buffer.
        public byte[] buffer = new byte[BufferSize];

        public byte[] GetData()
        {
            if (buffer == null || buffer.Length < 29) return new byte[] { };
            else
            {
                var result = new byte[buffer.Length - 28];
                for (int i = 28; i < buffer.Length; i++)
                    result[i - 28] = buffer[i];
                return result;
            }
        }
        // Received data string.
        public StringBuilder sb = new StringBuilder();

        public Dictionary<string, WifiInfoDevice> DevicesDictionary = new Dictionary<string, WifiInfoDevice>();

        public ObservableCollection<WifiInfoDevice> Devices = new ObservableCollection<WifiInfoDevice>();

        public bool Continue = true;
    }

}
