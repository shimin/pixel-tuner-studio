using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Common.Converters
{
    public class ControlVisibilityConverter : IValueConverter
    {
        #region Implementation of IValueConverter

        public object Convert(object value,
                              Type targetType, object parameter, CultureInfo culture)
        {
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
