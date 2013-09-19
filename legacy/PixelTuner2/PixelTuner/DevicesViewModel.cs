using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows;
using PixelTuner.Service;
using Common.Collections;
using Communication.Service;

namespace PixelTuner
{
    public class DevicesViewModel : ToolViewModel
    {
        #region Properties

        public const string ToolContentId = "DevicesTool";

        public SafeObservable<PixelDeviceViewModel> Devices
        {
            get;
            private set;
        }

        #endregion

        public DevicesViewModel(string name)
            : base(name)
        {
            IsVisible = true;
            ContentId = ToolContentId;

            Devices = new SafeObservable<PixelDeviceViewModel>();            
            DeviceHelper.Devices.ForEach((d) => { DeviceHelper_DeviceFound(new DeviceEventArgs(d)); });
            DeviceHelper.DeviceFound += new DeviceHelper.DeviceChangedDelegate(DeviceHelper_DeviceFound);
            DeviceHelper.DeviceLost += new DeviceHelper.DeviceChangedDelegate(DeviceHelper_DeviceLost);
        }
        
        void DeviceHelper_DeviceFound(DeviceEventArgs e)
        {
            var pixelDevice = e.Device as PixelDevice;
            if (pixelDevice != null)
            {
                Devices.Add(new PixelDeviceViewModel(pixelDevice));
            }
        }

        void DeviceHelper_DeviceLost(DeviceEventArgs e)
        {
            List<PixelDeviceViewModel> modelsToRemove = new List<PixelDeviceViewModel>();
            for (int i = 0; i < Devices.Count; i++)
            {
                if (Devices[i].Device == e.Device)
                {
                    modelsToRemove.Add(Devices[i]);
                    break;
                }
            }
            modelsToRemove.ForEach((item) => { Devices.Remove(item); });
        }
        
    }
}
