using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.ViewModel;
using Communication.Service;
using System.Windows.Input;

namespace PixelTuner.ViewModel
{
    public class IlluminatorDeviceViewModel : ViewModelBase
    {

        public IlluminatorDevice Device { get; private set; }

        public IlluminatorDeviceViewModel(IlluminatorDevice device)
            //: base(device)
        {
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
