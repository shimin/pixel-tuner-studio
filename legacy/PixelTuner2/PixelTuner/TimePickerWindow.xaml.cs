using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace PixelTuner
{
    /// <summary>
    /// Interaction logic for TimePickerWindow.xaml
    /// </summary>
    public partial class TimePickerWindow : Window, INotifyPropertyChanged
    {

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Properties

        private TimeSpan _t;
        public TimeSpan Time
        {
            get { return _t; }
            set
            {
                _t = value;
                RaisePropertyChanged("Time");
                Minutes = _t.Minutes;
                Seconds = _t.Seconds;
                Milliseconds = _t.Milliseconds;
            }
        }

        private int _m;
        public int Minutes
        {
            get { return _m; }
            set { _m = value; RaisePropertyChanged("Minutes"); }
        }

        private int _s;
        public int Seconds
        {
            get { return _s; }
            set { _s = value; RaisePropertyChanged("Seconds"); }
        }

        private int _ms;
        public int Milliseconds
        {
            get { return _ms; }
            set { _ms = value; RaisePropertyChanged("Milliseconds"); }
        }

        public FontWeight DurationWeight
        {
            get
            {
                var t = new TimeSpan(0, 0, Minutes, Seconds, Milliseconds);
                if (t != Time) return FontWeights.Bold;
                else return FontWeights.Normal;
            }
        }

        #endregion

        public TimePickerWindow()
        {
            InitializeComponent();
            DataContext = this;
            this.Closing += (s, e) => { Time = new TimeSpan(0, 0, Minutes, Seconds, Milliseconds); };
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            boxLengthMin.Focus();
        }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
