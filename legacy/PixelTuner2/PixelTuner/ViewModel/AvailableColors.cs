using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PixelTuner.ViewModel
{
    public static class AvailableColors
    {
        private static ObservableCollection<Toolkit.ColorItem> _colorsRGB = new ObservableCollection<Toolkit.ColorItem>();
        public static ObservableCollection<Toolkit.ColorItem> ColorsRgb
        {
            get
            {
                return _colorsRGB;
            }
            set
            {
                _colorsRGB = value;
            }
        }

        private static ObservableCollection<Toolkit.ColorItem> _colorsMono = new ObservableCollection<Toolkit.ColorItem>();
        public static ObservableCollection<Toolkit.ColorItem> ColorsMono
        {
            get
            {
                return _colorsMono;
            }
            set
            {
                _colorsMono = value;
            }
        }

        static AvailableColors()
        {
            _colorsRGB.Clear();
            _colorsMono.Clear();
            GenerateColors();
        }


        private static void GenerateColors()
        {
            byte step = 50;
            for (byte i = 255; i >= 0; i -= 5)
            {
                _colorsMono.Add(new Toolkit.ColorItem(Color.FromRgb(i, i, i), string.Format("{0}, {0}, {0}", i)));
            }

            for (byte r = 250; r >= 0; r -= step)
            {
                for (byte g = 250; g >= 0; g -= step)
                {
                    for (byte b = 250; b >= 0; b -= step)
                    {
                        _colorsRGB.Add(new Toolkit.ColorItem(Color.FromRgb(r, g, b), string.Format("{0}, {1}, {2}", r, g, b)));
                    }
                }
            }
        }

    }
}
