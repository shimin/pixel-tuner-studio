using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows;
using Common.Collections;
using Communication.Service;

namespace PixelTuner.ViewModel
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
            var pdvm = new PixelDeviceViewModel(pixelDevice);
            if (pixelDevice != null)
            {
                Devices.Add(pdvm);
            }
            if (pixelDevice.BootMode && ContinueUpdate())
            {
                var streamInfo = new System.Windows.Resources.StreamResourceInfo();

                if (pdvm.DeviceVersion == 2) streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Files/Flash.bin"));
                else if (pdvm.DeviceVersion == 3) streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Files/Flash_v3.bin"));

                pdvm.UploadFirmwareCommand.Execute(StreamHelper.ReadFully(streamInfo.Stream));
            }
        }

        private static bool ContinueUpdate()
        {
            return MessageBox.Show(
                "Continue update?", 
                "PixelTuner", 
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes;
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
