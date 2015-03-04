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
    /// Interaction logic for SplitItemWindow.xaml
    /// </summary>
    public partial class SplitItemWindow : Window, INotifyPropertyChanged
    {
        TimeLineItemViewModel item = null;

        public bool DurationChanged
        {
            get
            {
                return !(_initialDuration.Minutes == boxLengthMin.Value
                            && _initialDuration.Seconds == boxLengthSec.Value
                            && _initialDuration.Milliseconds == boxLengthMs.Value);
            }
        }
        
        private TimeSpan _initialDuration;
        public TimeSpan Duration { get; set; }

        public bool ForAll { get; set; }

        public bool NeedRefresh { get; set; }

        #region Controls style

        public FontWeight DurationWeight
        {
            get
            {
                return DurationChanged ? FontWeights.Bold : FontWeights.Normal;
            }
        }

        #endregion

        public SplitItemWindow(TimeLineItemViewModel context)
        {
            item = context;
            InitializeComponent();
            Initialize();
            DataContext = this;
            this.Closing += new System.ComponentModel.CancelEventHandler(SplitItemWindow_Closing);
        }
        
        private void Initialize()
        {
            //var duration = End - Start;
            var duration = new TimeSpan();
            boxLengthMin.Value = duration.Minutes;
            boxLengthSec.Value = duration.Seconds;
            boxLengthMs.Value = duration.Milliseconds;
            NeedRefresh = false;
        }

        void SplitItemWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Duration = new TimeSpan(0, 0, (int)boxLengthMin.Value, (int)boxLengthSec.Value, (int)boxLengthMs.Value);
            ForAll = (bool)boxForAll.IsChecked;
            NeedRefresh = Duration != new TimeSpan();
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
            RaisePropertyChanged("DurationWeight");
            RaisePropertyChanged("StretchingWeight");
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            boxLengthMin.Focus();
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
