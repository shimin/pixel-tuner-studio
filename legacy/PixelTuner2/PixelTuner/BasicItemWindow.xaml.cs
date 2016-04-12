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
using System.ComponentModel;
using PixelTuner.Controls.ThumbListView;

namespace PixelTuner
{
    /// <summary>
    /// Interaction logic for BasicItemWindow.xaml
    /// </summary>
    public partial class BasicItemWindow : Window, INotifyPropertyChanged
    {
        TimeLineItemViewModel item = null;

        private DateTime _initialStart;
        public DateTime Start { get; set; }

        public bool StartTimeChanged
        {
            get
            {
                return !(_initialStart.Minute == boxStartMin.Value
                        && _initialStart.Second == boxStartSec.Value
                        && _initialStart.Millisecond == boxStartMs.Value);
            }
        }

        public bool DurationChanged
        {
            get
            {
                return !(_initialDuration.Minutes == boxLengthMin.Value
                            && _initialDuration.Seconds == boxLengthSec.Value
                            && _initialDuration.Milliseconds == boxLengthMs.Value);
            }
        }
        
        public bool BrightnessChanged
        {
            get
            {
                return !(boxBrightness.Value == _initialBrightness);
            }
        }

        private DateTime _initialEnd;
        public DateTime End { get; set; }

        private TimeSpan _initialDuration;
        public TimeSpan Duration { get; set; }
        
        private byte _initialBrightness;
        public byte Brightness { get; set; }

        public bool ForAll { get; set; }

        public bool NeedRefresh { get; set; }

        #region Controls style

        public FontWeight StartWeight
        {
            get
            {
                return StartTimeChanged ? FontWeights.Bold : FontWeights.Normal;
            }
        }

        public FontWeight DurationWeight
        {
            get
            {
                return DurationChanged ? FontWeights.Bold : FontWeights.Normal;
            }
        }
        
        public FontWeight BrightnessWeight
        {
            get
            {
                return BrightnessChanged ? FontWeights.Bold : FontWeights.Normal;
            }
        }

        #endregion

        public BasicItemWindow(TimeLineItemViewModel context)
        {
            item = context;
            _initialStart = Start = context.StartTime.Value;
            _initialEnd = End = context.EndTime.Value;
            _initialDuration = _initialEnd - _initialStart;
            _initialBrightness = Brightness = context.Brightness;
            InitializeComponent();
            Initialize();
            DataContext = this;
            this.Closing += new System.ComponentModel.CancelEventHandler(BasicItemWindow_Closing);
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

            boxBrightness.Value = Brightness;
            NeedRefresh = false;
        }

        void BasicItemWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Start = new DateTime(Start.Year, Start.Month, Start.Day, Start.Hour, (int)boxStartMin.Value, (int)boxStartSec.Value, (int)boxStartMs.Value);
            Duration = new TimeSpan(0, 0, (int)boxLengthMin.Value, (int)boxLengthSec.Value, (int)boxLengthMs.Value);
            End = Start + Duration;

            Brightness = (byte)boxBrightness.Value;
            ForAll = (bool)boxForAll.IsChecked;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            NotifyBindings();
            switch (e.Key)
            {
                case Key.Escape: e.Handled = true; this.Close(); break;
                case Key.Enter: e.Handled = true; this.DialogResult = true; this.Close(); break;
            }
        }

        private void NotifyBindings()
        {
            RaisePropertyChanged("StartWeight");
            RaisePropertyChanged("DurationWeight");
            RaisePropertyChanged("StretchingWeight");
            RaisePropertyChanged("BrightnessWeight");
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            boxStartMin.Focus();
        }

        #region INotifyPropertyChanged Members

        protected virtual void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        private void numericValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            NotifyBindings();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true; 
            this.Close(); 
        }

    }
}
