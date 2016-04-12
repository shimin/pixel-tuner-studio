using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PixelTuner.ViewModel
{
    public class EffectSquareThumbnailViewModel : EffectThumbnailViewModel
    {
        public EffectSquareThumbnailViewModel()
            : this(null, "")
        {

        }

        public EffectSquareThumbnailViewModel(BitmapImage img, string fullName)
            : base(img, fullName)
        {
            _uri = "pack://application:,,,/Effects/02_effect.png";
            ColorFirst = Color.FromRgb(0, 0, 0);
            ColorLast = Color.FromRgb(255, 0, 0);
            T1 = 1000;
            T2 = 1000;
        }
        
        protected override void ProprietaryEdit(object parameter)
        {
            EffectSquareWindow w = new EffectSquareWindow(this);
            var location = Application.Current.MainWindow.PointToScreen(Mouse.GetPosition(Application.Current.MainWindow));
            w.Left = location.X - w.Width / 2;
            w.Top = location.Y - w.Height / 2;
            w.ShowDialog();
            if (w.NeedRefresh) RootViewModel.NotifySettingsChanged();
        }

        public override object Clone()
        {
            EffectSquareThumbnailViewModel result = (EffectSquareThumbnailViewModel)base.Clone();
            result.T1 = this.T1;
            result.T2 = this.T2;
            result.ColorFirst = this.ColorFirst;
            result.ColorLast = this.ColorLast;
            return result;
        }
    }
}
