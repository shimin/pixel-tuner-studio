using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;
using PixelTuner.ViewModel;
using Common.ViewModel;
using NAudio;
using System.Windows;
using System.ComponentModel;

namespace PixelTuner.Styles
{
    public partial class FileLayoutTemplate : System.Windows.ResourceDictionary
    {
        private Grid gridControl = null;
        private ScrollViewer _waveformScroll = null;

        private void Grid_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            var sv = ((Grid)sender).FindName("boxScroll") as ScrollViewer;
            if (sv != null)
            {
                if (IsCtrlPressed() && e.Delta < 0)
                {
                    sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta);
                    e.Handled = true;
                }
                if (IsCtrlPressed() && e.Delta > 0)
                {
                    sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta);
                    e.Handled = true;
                }
                
                if (IsAltPressed())
                {
                    var fm = RootViewModel.This.ActiveDocument as FileViewModel;

                    var delta = FileViewModel.GetScaleRate((double)e.Delta, fm.UnitSize);

                    if (fm.UnitSize + delta >= fm.MinUnitSize && fm.UnitSize + delta <= fm.MaxUnitSize)
                        fm.UnitSize += delta;
                    e.Handled = true;
                }
            }
        }

        private bool IsAltPressed()
        {
            return System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftAlt) ||
                System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightAlt);
        }

        private bool IsCtrlPressed()
        {
            return System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl) ||
                System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightCtrl);
        }

        private bool IsShiftPressed()
        {
            return System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift) ||
                System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightShift);
        }

        private void Grid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            FileViewModel fm = null;
            if (e.Key == System.Windows.Input.Key.Delete)
            {
                if (fm == null) fm = RootViewModel.This.ActiveDocument as FileViewModel;
                fm.DeleteSelectedItems();
            }
            if (e.Key == System.Windows.Input.Key.C && IsCtrlPressed())
            {
                if (fm == null) fm = RootViewModel.This.ActiveDocument as FileViewModel;
                fm.CopySelectedItems();
            }
            if (e.Key == System.Windows.Input.Key.V && IsCtrlPressed())
            {
                if (fm == null) fm = RootViewModel.This.ActiveDocument as FileViewModel;
                fm.PasteSelectedItems();
            }
            // first handled by MainForm than by GridControl...
            // MainForm -> ActiveDocumentLayout -> Grid -> TimeLineControl -> TimeLineItem
        }

        private void Grid_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {

        }

        private void Grid_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var dc = ((Grid)(sender));
            try
            {
                var element = VisualTreeHelper.HitTest(dc, e.GetPosition(dc)).VisualHit as System.Windows.FrameworkElement;
                var fm = RootViewModel.This.ActiveDocument as FileViewModel;
                if (element != null && (element.DataContext is TimeLineViewModel || element.DataContext is TimeLineItemViewModel))
                {
                    fm.SetSelection(element.DataContext, IsCtrlPressed(), IsShiftPressed(), IsAltPressed());
                }
            }
            catch { }
        }
        
        private void boxGrid_RequestBringIntoView(object sender, System.Windows.RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }

        private void StackPanel_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var panel = sender as StackPanel;
            if (panel != null) panel.Visibility = System.Windows.Visibility.Hidden;
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.HorizontalChange != 0)
            {
                RootViewModel.This.ActiveDocument.HorizontalScrollPosition = e.HorizontalOffset;
            }
            e.Handled = false;
        }
        
        private void FileLayoutTemplate_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if(e.PropertyName == "PlaybackPointerPosition")
            {
                if (_waveformScroll != null)
                {
                    FileViewModel vm = _waveformScroll.DataContext as FileViewModel;
                    if (vm != null) _waveformScroll.ScrollToHorizontalOffset(vm.PlaybackPointerPosition);
                }
            }
        }

        private void boxScroll_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _waveformScroll = sender as ScrollViewer;
            if (_waveformScroll != null)
            {
                (_waveformScroll.DataContext as INotifyPropertyChanged).PropertyChanged += FileLayoutTemplate_PropertyChanged;
            }
        }
    }
}
