using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PixelTuner.ViewModel
{
    public class EffectSineThumbnailViewModel : EffectThumbnailViewModel
    {
        public EffectSineThumbnailViewModel()
            : this(null, "")
        {

        }

        public EffectSineThumbnailViewModel(BitmapImage img, string fullName)
            : base(img, fullName)
        {
            ColorFirst = Color.FromRgb(0, 0, 0);
            ColorLast = Color.FromRgb(255, 0, 0);
            T1 = 2000;
            T2 = null;
        }

        protected override void OnThumbnail(object parameter)
        {
            EffectSineWindow w = new EffectSineWindow(this);
            var location = Application.Current.MainWindow.PointToScreen(Mouse.GetPosition(Application.Current.MainWindow));
            w.Left = location.X - w.Width / 2;
            w.Top = location.Y - w.Height / 2;
            if (w.ShowDialog() == true)
            {
                //ApplyChanges(w.Start, w.Duration, w.Stretching, w.Brightness, w.ForAll, w.DurationChanged, w.StretchingChanged, w.BrightnessChanged);
            }
            if (w.NeedRefresh) RootViewModel.NotifySettingsChanged();
            //(this.Parent as TimeLineViewModel).IsChanged = true;
        }

        protected override bool CanThumbnail(object parameter)
        {
            return true;
        }

        public override object Clone()
        {
            EffectSineThumbnailViewModel result = new EffectSineThumbnailViewModel((BitmapImage)ImageSource, Name);
            result.StartTime = this.StartTime;
            result.EndTime = this.EndTime;
            result.T1 = this.T1;
            result.T2 = this.T2;
            result.TimelineViewExpanded = this.TimelineViewExpanded;
            result.Stretching = this.Stretching;
            result.NormalBackground = this.NormalBackground;
            result.ColorFirst = this.ColorFirst;
            result.ColorLast = this.ColorLast;
            return result;
        }
    }
}
