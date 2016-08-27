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
        public override string ContentID
        {
            get
            {
                return "Sine Wave";
            }
        }

        public EffectSineThumbnailViewModel()
            : this(null, "")
        {

        }

        public EffectSineThumbnailViewModel(BitmapImage img, string fullName)
            : base(img, fullName)
        {
            _uri = "pack://application:,,,/Effects/01_effect.png";
            ColorFirst = Color.FromRgb(0, 0, 0);
            ColorLast = Color.FromRgb(255, 0, 0);
            T1 = 2000;
            T2 = null;
        }
        
        protected override void ProprietaryEdit(object parameter)
        {
            EffectSineWindow w = new EffectSineWindow(this);
            var location = Application.Current.MainWindow.PointToScreen(Mouse.GetPosition(Application.Current.MainWindow));
            w.Left = location.X - w.Width / 2;
            w.Top = location.Y - w.Height / 2;
            w.ShowDialog();
            if (w.NeedRefresh) RootViewModel.NotifySettingsChanged();
        }

        public override object Clone()
        {
            EffectSineThumbnailViewModel result = (EffectSineThumbnailViewModel)base.Clone();
            result.T1 = this.T1;
            result.T2 = this.T2;
            result.ColorFirst = this.ColorFirst;
            result.ColorLast = this.ColorLast;
            return result;
        }
    }
}
