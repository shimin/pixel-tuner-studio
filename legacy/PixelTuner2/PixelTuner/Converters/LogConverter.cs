using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Globalization;
using System.Windows;

namespace PixelTuner.Converters
{
    [ValueConversion(typeof(double), typeof(double))]
    public class LogConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        { 
            var newValue = ((double)value) / 0.02;
            return (double)Math.Sqrt(newValue);
            
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var v = (double)value;

            if ((double)value > 0)
                return 0.02 * v * v;
            else
                return 0.0;
        }
    }
}
