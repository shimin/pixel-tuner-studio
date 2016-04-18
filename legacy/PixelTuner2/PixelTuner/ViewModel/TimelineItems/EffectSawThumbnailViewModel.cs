using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using System.Windows.Input;

namespace PixelTuner.ViewModel
{
    public class EffectSawThumbnailViewModel : EffectThumbnailViewModel
    {
        public override string ContentID
        {
            get
            {
                return "Saw-tooth Wave";
            }
        }

        public EffectSawThumbnailViewModel()
            : this(null, "")
        {

        }

        public EffectSawThumbnailViewModel(BitmapImage img, string fullName)
            : base(img, fullName)
        {
            _uri = "pack://application:,,,/Effects/03_effect.png";
            ColorFirst = Color.FromRgb(0, 0, 0);
            ColorLast = Color.FromRgb(255, 0, 0);
            T1 = 1000;
            T2 = 1000;
        }
        
        protected override void ProprietaryEdit(object parameter)
        {
            EffectSawWindow w = new EffectSawWindow(this);
            var location = Application.Current.MainWindow.PointToScreen(Mouse.GetPosition(Application.Current.MainWindow));
            w.Left = location.X - w.Width / 2;
            w.Top = location.Y - w.Height / 2;
            w.ShowDialog();
            if (w.NeedRefresh) RootViewModel.NotifySettingsChanged();
        }
        
        public override object Clone()
        {
            EffectSawThumbnailViewModel result = (EffectSawThumbnailViewModel)base.Clone();
            result.T1 = this.T1;
            result.T2 = this.T2;
            result.ColorFirst = this.ColorFirst;
            result.ColorLast = this.ColorLast;
            return result;
        }
    }
}
