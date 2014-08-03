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

namespace PixelTuner
{
    /// <summary>
    /// Interaction logic for ItemWindow.xaml
    /// </summary>
    public partial class ItemWindow : Window, INotifyPropertyChanged
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
                if (UseTimeDuration)
                    return !(_initialDuration.Minutes == boxLengthMin.Value
                            && _initialDuration.Seconds == boxLengthSec.Value
                            && _initialDuration.Milliseconds == boxLengthMs.Value);
                else
                    return !(_initialEnd.Minute == boxLengthMin.Value
                            && _initialEnd.Second == boxLengthSec.Value
                            && _initialEnd.Millisecond == boxLengthMs.Value);
            }
        }

        public bool StretchingChanged
        {
            get
            {
                return !(boxStretching.Value == _initialStretching);
            }
        }

        public bool BrightnessChanged
        {
            get
            {
                return !(boxBrightness.Value == _initialBrightness);
            }
        }

        public bool LaserOnChanged
        {
            get
            {
                return !(boxLaserOn.IsChecked == _initialLaserOn);
            }
        }

        private bool _initialLaserOn;
        public bool LaserOn { get; set; }

        private DateTime _initialEnd;
        public DateTime End { get; set; }

        private TimeSpan _initialDuration;
        public TimeSpan Duration { get; set; }

        private int _initialStretching;
        public int Stretching { get; set; }

        private byte _initialBrightness;
        public byte Brightness { get; set; }

        public bool ForAll { get; set; }

        public bool NeedRefresh { get; set; }

        private bool _useTimeDuration;
        public bool UseTimeDuration
        {
            get { return _useTimeDuration; }
            set { _useTimeDuration = value; RaisePropertyChanged("UseTimeDuration"); RaisePropertyChanged("DurationChanged"); }
        }

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

        public FontWeight StretchingWeight
        {
            get
            {
                return StretchingChanged ? FontWeights.Bold : FontWeights.Normal;
            }
        }

        public FontWeight BrightnessWeight
        {
            get
            {
                return BrightnessChanged ? FontWeights.Bold : FontWeights.Normal;
            }
        }

        public FontWeight LaserOnWeight
        {
            get
            {
                return LaserOnChanged ? FontWeights.Bold : FontWeights.Normal;
            }
        }
        #endregion

        public ItemWindow(TimeLineItemViewModel context)
        {
            item = context;
            _initialStart = Start = context.StartTime.Value;
            _initialEnd = End = context.EndTime.Value;
            _initialDuration = _initialEnd - _initialStart;
            _initialStretching = Stretching = context.Stretching;
            _initialBrightness = Brightness = context.Brightness;
            _initialLaserOn = LaserOn = context.LaserOn;
            InitializeComponent();
            Initialize();
            DataContext = this;
            this.Closing += new System.ComponentModel.CancelEventHandler(ItemWindow_Closing);
        }
        
        private void Initialize()
        {
            UseTimeDuration = PixelTuner.Properties.Settings.Default.TimeSpanMode;

            boxStartMin.Value = Start.Minute;
            boxStartSec.Value = Start.Second;
            boxStartMs.Value = Start.Millisecond;

            SetDurationControlValues();

            boxStretching.Value = Stretching;
            boxBrightness.Value = Brightness;
            NeedRefresh = false;
        }

        private void SetDurationControlValues()
        {
            var duration = End - Start;
            if (!UseTimeDuration)
            {
                boxLengthMin.Value = End.Minute;
                boxLengthSec.Value = End.Second;
                boxLengthMs.Value = End.Millisecond;
            }
            else
            {
                boxLengthMin.Value = duration.Minutes;
                boxLengthSec.Value = duration.Seconds;
                boxLengthMs.Value = duration.Milliseconds;
            }
        }

        private void GetDuraionControlValues()
        {
            if (UseTimeDuration)
            {
                Duration = new TimeSpan(0, 0, (int)boxLengthMin.Value, (int)boxLengthSec.Value, (int)boxLengthMs.Value);
                End = Start + Duration;
            }
            else
            {
                End = new DateTime(Start.Year, Start.Month, Start.Day, Start.Hour, (int)boxLengthMin.Value, (int)boxLengthSec.Value, (int)boxLengthMs.Value);
                Duration = End - Start;
            }
        }

        void ItemWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Start = new DateTime(Start.Year, Start.Month, Start.Day, Start.Hour, (int)boxStartMin.Value, (int)boxStartSec.Value, (int)boxStartMs.Value);
            GetDuraionControlValues();

            Stretching = (int)boxStretching.Value;
            Brightness = (byte)boxBrightness.Value;
            ForAll = (bool)boxForAll.IsChecked;

            NeedRefresh = Brightness != item.Brightness;
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

        private void controlValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            NotifyBindings();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true; 
            this.Close(); 
        }

        private void boxLaserOn_Checked(object sender, RoutedEventArgs e)
        {
            RaisePropertyChanged("LaserOnWeight");
        }

        private void boxLaserOn_Unchecked(object sender, RoutedEventArgs e)
        {
            RaisePropertyChanged("LaserOnWeight");
        }

        private void boxSwitchTimeMode_Click(object sender, RoutedEventArgs e)
        {
            GetDuraionControlValues();
            UseTimeDuration = !UseTimeDuration;
            SetDurationControlValues();
        }

    }
}
