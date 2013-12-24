using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PixelTuner.ViewModel;

namespace PixelTuner
{
    /// <summary>
    /// Interaction logic for ItemWindow.xaml
    /// </summary>
    public partial class ItemWindow : Window
    {
        TimeLineItemViewModel item = null;

        public DateTime Start { get; set; }
        
        public DateTime End { get; set; }

        public TimeSpan Duration { get; set; }

        public int Stretching { get; set; }

        public byte Brightness { get; set; }

        public bool ForAll { get; set; }

        public bool NeedRefresh { get; set; }

        public ItemWindow(TimeLineItemViewModel context)
        {
            item = context;
            Start = context.StartTime.Value;
            End = context.EndTime.Value;
            Stretching = context.Stretching;
            Brightness = context.Brightness;
            InitializeComponent();
            Initialize();
            this.Closing += new System.ComponentModel.CancelEventHandler(ItemWindow_Closing);
        }
        
        private void Initialize()
        {
            boxStartMin.Value = Start.Minute;
            boxStartSec.Value = Start.Second;
            boxStartMs.Value = Start.Millisecond;

            var duration = End - Start;
            boxLengthMin.Value = duration.Minutes;
            boxLengthSec.Value = duration.Seconds;
            boxLengthMs.Value = duration.Milliseconds;

            boxStretching.Value = Stretching;
            boxBrightness.Value = Brightness;
            NeedRefresh = false;
        }

        void ItemWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Start = new DateTime(Start.Year, Start.Month, Start.Day, Start.Hour, (int)boxStartMin.Value, (int)boxStartSec.Value, (int)boxStartMs.Value);
            Duration = new TimeSpan(0, 0, (int)boxLengthMin.Value, (int)boxLengthSec.Value, (int)boxLengthMs.Value);
            End = Start + Duration;

            Stretching = (int)boxStretching.Value;
            Brightness = (byte)boxBrightness.Value;
            ForAll = (bool)boxForAll.IsChecked;

            NeedRefresh = Brightness != item.Brightness;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape: e.Handled = true; this.Close(); break;
                case Key.Enter: e.Handled = true; this.DialogResult = true; this.Close(); break;
            }
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            boxStartMin.Focus();
        }
    }
}
