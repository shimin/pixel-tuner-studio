using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using PixelTuner.ViewModel;
using PixelTuner.Controls;

namespace PixelTuner.Styles
{
    public partial class TimeLineLayoutTemplate : ResourceDictionary
    {
        void grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var g = (Grid)sender;
            var timelines = g.Children.OfType<Common.Controls.TimeLineControl>().ToList();
            foreach (var t in timelines)
                t.Width = t.MinWidth = g.ActualWidth - 26;
        }

        private void boxImageButton_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MessageBox.Show("");
        }

        private void ChildPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var s = sender as IInputElement;
            if (s != null)
            {
                s.CaptureMouse();
                s.ReleaseMouseCapture();
            }
        }
    }
}
