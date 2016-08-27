using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.ViewModel;
using Communication.Service;
using System.Windows.Input;
using System.Windows;

namespace PixelTuner.ViewModel
{
    public class IlluminatorDeviceViewModel : ViewModelBase
    {
        public IlluminatorDevice Device { get; private set; }

        private int FirmwareFileVersion { get; set; }

        private byte[] _firmwareBytes;

        public IlluminatorDeviceViewModel()
        {
            var streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Files/Flash_IL.bin"));
            _firmwareBytes = Common.Collections.StreamHelper.ReadFully(streamInfo.Stream);
            FirmwareFileVersion = _firmwareBytes[52];
        }

        public IlluminatorDeviceViewModel(IlluminatorDevice device)
        {
            Device = device;
        }

        public void Play(TimeSpan time, int discreteness)
        {
            int ms = (int)time.TotalMilliseconds;
            ms = (ms / discreteness) * discreteness;

            if (Device is IlluminatorDevice)
                (this.Device as IlluminatorDevice).PlayCommand(TimeSpan.FromMilliseconds(ms));
        }

        public void Pause(TimeSpan time, int discreteness)
        {
            int ms = (int)time.TotalMilliseconds;
            ms = (ms / discreteness) * discreteness;

            if (Device is IlluminatorDevice)
                (this.Device as IlluminatorDevice).PauseCommand(TimeSpan.FromMilliseconds(ms));
        }

        public void Stop(TimeSpan time, int discreteness)
        {
            int ms = (int)time.TotalMilliseconds;
            ms = (ms / discreteness) * discreteness;

            if (Device is IlluminatorDevice)
                (this.Device as IlluminatorDevice).StopCommand(TimeSpan.FromMilliseconds(ms));
        }

        public void Seek(TimeSpan time, int discreteness)
        {
            int ms = (int)time.TotalMilliseconds;
            ms = (ms / discreteness) * discreteness;

            if (Device is IlluminatorDevice)
                (this.Device as IlluminatorDevice).SeekCommand(TimeSpan.FromMilliseconds(ms));
        }
    }
}
