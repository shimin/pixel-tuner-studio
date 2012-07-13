using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows;
using PixelTuner.Service;

namespace PixelTuner
{
    public class DevicesViewModel : ToolViewModel
    {
        public DevicesViewModel(string name)
            : base(name)
        {
            ContentId = ToolContentId;
            IsVisible = true;
        }

        public const string ToolContentId = "DevicesTool";

        private void MenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {

        }

    }
}
