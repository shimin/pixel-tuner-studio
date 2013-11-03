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
            var timelines = g.Children.OfType<TimeLineControl>().ToList();
            foreach (var t in timelines)
                t.Width = t.MinWidth = g.ActualWidth - 26;
        }
    }
}
