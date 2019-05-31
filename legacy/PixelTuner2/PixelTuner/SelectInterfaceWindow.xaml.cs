using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PixelTuner.Properties;

namespace PixelTuner
{
    public static class EnumHelper
    {
        public static string GetDescription<T>(this T enumerationValue)
            where T : struct
        {
            Type type = enumerationValue.GetType();
            if (!type.IsEnum)
            {
                throw new ArgumentException("EnumerationValue must be of Enum type", "enumerationValue");
            }

            //Tries to find a DescriptionAttribute for a potential friendly name
            //for the enum
            MemberInfo[] memberInfo = type.GetMember(enumerationValue.ToString());
            if (memberInfo != null && memberInfo.Length > 0)
            {
                object[] attrs = memberInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);

                if (attrs != null && attrs.Length > 0)
                {
                    //Pull out the description value
                    return ((DescriptionAttribute)attrs[0]).Description;
                }
            }
            //If we have no description attribute, just return the ToString of the enum
            return enumerationValue.ToString();
        }
    }

    internal class InterfaceInfo
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

    /// <summary>
    /// Interaction logic for SelectInterfaceWindow.xaml
    /// </summary>
    public partial class SelectInterfaceWindow : Window
    {
        IPEndPoint _ep = null;

        public SelectInterfaceWindow()
        {
            InitializeComponent();
            Closing += MainWindow_Closing;
            InitializeNetworkList();
        }

        public IPEndPoint InterfaceSelected
        {
            get { return _ep; }
        }

        private void BoxInterfaces_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            InitializeInterface();
        }

        private void InitializeNetworkList()
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            for (int i = 0; i < interfaces.Length; i++)
            {
                if (interfaces[i].OperationalStatus == OperationalStatus.Up &&
                    (interfaces[i].NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                     interfaces[i].NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                {
                    boxInterfaces.Items.Add(new InterfaceInfo(interfaces[i]));
                }
            }
            if (boxInterfaces.Items.Count > 0) boxInterfaces.SelectedIndex = Settings.Default.InterfaceIdx;
        }

        private void InitializeInterface()
        {
            try
            {
                if (boxInterfaces.SelectedIndex != -1)
                {
                    var ni = boxInterfaces.SelectedItem as InterfaceInfo;
                    var ipProps = ni.NetworkInterfaceInfo.GetIPProperties();
                    string ipstr = "0.0.0.0";

                    foreach (var ip in ipProps.UnicastAddresses)
                    {
                        if ((ni.NetworkInterfaceInfo.OperationalStatus == OperationalStatus.Up)
                            && (ip.Address.AddressFamily == AddressFamily.InterNetwork))
                        {
                            ipstr = ip.Address.ToString();
                        }
                    }
                    ipstr = ipstr.Substring(0, ipstr.LastIndexOf(".")) + ".255";
                    _ep = new IPEndPoint(IPAddress.Parse(ipstr), 333);
                }
            }
            catch
            {
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Settings.Default.InterfaceIdx = boxInterfaces.SelectedIndex;
            Settings.Default.Save();
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
