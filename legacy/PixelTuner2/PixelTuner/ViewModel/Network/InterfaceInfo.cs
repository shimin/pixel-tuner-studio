using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace PixelTuner.ViewModel.Network
{
    public class InterfaceInfo
    {
        private NetworkInterface _nIface;

        public NetworkInterface NetworkInterfaceInfo
        {
            get { return _nIface; }
        }

        public InterfaceInfo(NetworkInterface niface)
        {
            _nIface = niface;
        }

        public override string ToString()
        {
            string name = "Unknown";
            if (_nIface != null)
            {
                var ipProps = _nIface.GetIPProperties();

                foreach (var ip in ipProps.UnicastAddresses)
                {
                    if ((_nIface.OperationalStatus == OperationalStatus.Up)
                        && (ip.Address.AddressFamily == AddressFamily.InterNetwork))
                    {
                        name = _nIface.Description + " | " + ip.Address.ToString();
                    }
                }
            }
            return name;
        }
    }

}
