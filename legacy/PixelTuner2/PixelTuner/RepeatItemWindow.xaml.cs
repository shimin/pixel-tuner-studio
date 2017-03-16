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
    public partial class RepeatItemWindow : Window, INotifyPropertyChanged
    {
        TimeLineItemViewModel item = null;

        public int Stretching
        {
            get;
            private set;
        }

        public RepeatItemWindow(TimeLineItemViewModel context)
        {
            item = context;
            Stretching = -1;
            InitializeComponent();
            DataContext = this;
            this.Closing += new System.ComponentModel.CancelEventHandler(SplitItemWindow_Closing);
        }

        void SplitItemWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DialogResult == true)
            {
                var duration = item.EndTime.Value - item.StartTime.Value;
                //var repCount = duration / (item.ImageSource.PixelWidth * K)
                var maxRepeatCount = duration.TotalMilliseconds/item.ImageSource.PixelWidth/4;
                var minRepeatCount = duration.TotalMilliseconds/item.ImageSource.PixelWidth/255;
                //in case of K is milliseconds to display one pixel column
                var K = (duration.TotalMilliseconds/boxRepeatCount.Value)/item.ImageSource.PixelWidth;
                if (K >= 3 && K <= 255)
                {
                    Stretching = (int) K;
                }
                else
                {
                    MessageBox.Show(
                        $"Value can not be more than {maxRepeatCount} and less than {Math.Max(1, minRepeatCount)} for such length of picture.");
                }
                /*
                // актуально только для 50-ок
                Duration d = item.EndTime.Value - item.StartTime.Value;
                double minMcsLength = item.ImageSource.PixelWidth * 0.1;

                int maxRepeatCount = (int)(d.TimeSpan.TotalMilliseconds / minMcsLength);
                if ((d.TimeSpan.TotalMilliseconds % minMcsLength) > 0) maxRepeatCount++;
                
                int targetStretching = maxRepeatCount / (int)boxRepeatCount.Value;
                if (targetStretching >= 3 && targetStretching <= 255)
                {
                    Stretching = targetStretching;
                }
                else
                {
                    MessageBox.Show(String.Format(
                        "Value can not be more than {0} and less than {1} for such length of picture."
                        , maxRepeatCount
                        , Math.Max(1, (int)maxRepeatCount / 255)));
                }*/
            }
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
            boxRepeatCount.Focus();
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
