using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace PixelTuner.Styles
{
    public partial class FileLayoutTemplate : System.Windows.ResourceDictionary
    {
        bool ctrlHold = false;
        bool altHold = false;

        private void Grid_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            var sv = ((Grid)sender).FindName("boxScroll") as ScrollViewer;
            if (sv != null)
            {
                if (ctrlHold && e.Delta < 0)
                {
                    sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta);
                    e.Handled = true;
                }
                if (ctrlHold && e.Delta > 0)
                {
                    sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta);
                    e.Handled = true;
                }
                if (altHold)
                {
                    var fm = ((Grid)sender).DataContext as PixelTuner.ViewModel.FileViewModel;
                    var delta = (double)e.Delta / 50;
                    if (fm.UnitSize + delta >= fm.MinUnitSize && fm.UnitSize + delta <= fm.MaxUnitSize)
                        fm.UnitSize += delta;
                    e.Handled = true;
                }
            }
        }

        private void Grid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.LeftCtrl || e.Key == System.Windows.Input.Key.RightCtrl)
            {
                ctrlHold = true;
                e.Handled = true;
            }
            if (e.SystemKey == System.Windows.Input.Key.LeftAlt || e.SystemKey == System.Windows.Input.Key.RightAlt)
            {
                altHold = true;
                e.Handled = true;
            }
        }

        private void Grid_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.LeftCtrl || e.Key == System.Windows.Input.Key.RightCtrl)
            {
                ctrlHold = false;
                e.Handled = true;
            }
            if (e.SystemKey == System.Windows.Input.Key.LeftAlt || e.SystemKey == System.Windows.Input.Key.RightAlt)
            {
                altHold = false;
                e.Handled = true;
            }
        }
    }
}
