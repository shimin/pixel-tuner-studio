using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PixelTuner.Converters
{
    public class ThumbnailConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var source = (ImageSource)value;
            Rect _r = new Rect();
            if (source != null)
            {
                var _k = source.Height / 44;

                _r = new Rect(0, 0, source.Width / _k, source.Height / _k);
            }
            return _r;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
