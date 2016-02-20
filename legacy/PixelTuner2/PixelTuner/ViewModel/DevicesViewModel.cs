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

        SafeObservable<IlluminatorDeviceViewModel> _illuminators = new SafeObservable<IlluminatorDeviceViewModel>();
        public SafeObservable<IlluminatorDeviceViewModel> Illuminators
        {
            get { return _illuminators; }
        }

        public SafeObservable<LightMasterDeviceViewModel> LightMasterDevices
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
            LightMasterDevices = new SafeObservable<LightMasterDeviceViewModel>();

            DeviceHelper.Devices.ForEach((d) => { DeviceHelper_DeviceFound(new DeviceEventArgs(d)); });
            DeviceHelper.DeviceFound += new DeviceHelper.DeviceChangedDelegate(DeviceHelper_DeviceFound);
            DeviceHelper.DeviceLost += new DeviceHelper.DeviceChangedDelegate(DeviceHelper_DeviceLost);
        }

        void DeviceHelper_DeviceFound(DeviceEventArgs e)
        {
            if (e.Device is LightMasterDevice)
            {
                var model = new LightMasterDeviceViewModel((LightMasterDevice)e.Device);
                LightMasterDevices.Add(model);
            }
            else if (e.Device is IlluminatorDevice)
            {
                var model = new IlluminatorDeviceViewModel((IlluminatorDevice)e.Device);
                Illuminators.Add(model);
            }
            else if (e.Device is PixelDevice)
            {
                var model = new PixelDeviceViewModel(e.Device as PixelDevice);
                Devices.Add(model);
            }
            else if (e.Device is GenericSerialDevice)
            {
                //var lightMasterDevice = new LightMasterDevice((e.Device as GenericSerialDevice).Port);
                //lightMasterDevice.Initialize(null);

                //var model = new PixelDeviceViewModel(lightMasterDevice);
                //Devices.Add(model);
            }
        }

        void DeviceHelper_DeviceLost(DeviceEventArgs e)
        {
            List<PixelDeviceViewModel> modelsToRemove = new List<PixelDeviceViewModel>();
            for (int i = 0; i < Devices.Count; i++)
            {
                if (Devices[i].Device.Equals(e.Device))
                {
                    modelsToRemove.Add(Devices[i]);
                    break;
                }
            }
            modelsToRemove.ForEach((item) => { Devices.Remove(item); });

            List<IlluminatorDeviceViewModel> illuminatorsToRemove = new List<IlluminatorDeviceViewModel>();
            for (int i = 0; i < Illuminators.Count; i++)
            {
                if (Illuminators[i].Device.Equals(e.Device))
                {
                    illuminatorsToRemove.Add(Illuminators[i]);
                    break;
                }
            }
            illuminatorsToRemove.ForEach((item) => { Illuminators.Remove(item);});


            List<LightMasterDeviceViewModel> lMastersToRemove = new List<LightMasterDeviceViewModel>();
            for (int i = 0; i < LightMasterDevices.Count; i++)
            {
                if (LightMasterDevices[i].Device.Equals(e.Device))
                {
                    lMastersToRemove.Add(LightMasterDevices[i]);
                    break;
                }
            }
            lMastersToRemove.ForEach((item) => { LightMasterDevices.Remove(item); });
        }

        private static bool ContinueUpdate()
        {
            return MessageBox.Show(
                "Continue update?", 
                "PixelTuner", 
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes;
        }
    }
}
