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
                // Called by WPF while rendering the interface list. If the adapter was
                // disabled or removed after the list was built, GetIPProperties() throws
                // NetworkInformationException, which must not escape into layout.
                try
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
                catch (Exception)
                {
                    try { name = _nIface.Description; }
                    catch (Exception) { }
                }
            }
            return name;
        }
    }

}
