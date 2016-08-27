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
    /// Interaction logic for TimelinesToUpload.xaml
    /// </summary>
    public partial class TimelinesToUpload : Window, INotifyPropertyChanged
    {
        protected virtual void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        public event PropertyChangedEventHandler PropertyChanged;

        IEnumerable<TimeLineViewModel> _timeLines = null;
        public IEnumerable<TimeLineViewModel> TimeLines
        {
            get { return _timeLines; }
            set { _timeLines = value; RaisePropertyChanged("TimeLines"); }
        }

        public IEnumerable<TimeLineViewModel> SelectedTimeLines
        {
            get
            {
                return _timeLines.Where(i => i.IsSelectedToUpload);
            }
        }

        public TimelinesToUpload(Func<TimeLineViewModel, bool> filter)
        {
            DataContext = this;
            TimeLines = RootViewModel.This.ActiveDocument.TimeLines.Where(filter);
            InitializeComponent();
            //DataContext = RootViewModel.This.ActiveDocument;

            //if (timelineType == typeof(PixelDeviceViewModel))
            //{
            //    TimeLines = RootViewModel.This.ActiveDocument.TimeLines.Where(
            //        i => i is PixelTimeLineViewModel && ((PixelTimeLineViewModel)i).LineHeight == lineHeight);
            //}
            //else if (timelineType == typeof(LightMasterDeviceViewModel))
            //{
            //    TimeLines = RootViewModel.This.ActiveDocument.TimeLines.Where(i => i is LightTimeLineViewModel);
            //}
            //else if (timelineType == typeof(EpicPoiDeviceViewModel))
            //{
            //    TimeLines = RootViewModel.This.ActiveDocument.TimeLines.Where(i => i is EpicPoiTimeLineViewModel);
            //}
        }

        private void buttonOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void boxSelect_Click(object sender, RoutedEventArgs e)
        {
            foreach (var t in TimeLines)
                t.IsSelectedToUpload = true;
        }

        private void boxDeselect_Click(object sender, RoutedEventArgs e)
        {
            foreach (var t in TimeLines)
                t.IsSelectedToUpload = false;
        }
    }
}
