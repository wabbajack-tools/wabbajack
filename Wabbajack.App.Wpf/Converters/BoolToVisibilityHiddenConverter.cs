using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Wabbajack
{
    [ValueConversion(typeof(Visibility), typeof(bool))]
    public class BoolToVisibilityHiddenConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType != typeof(Visibility))
                throw new InvalidOperationException($"The target must be of type {nameof(Visibility)}");
            bool compareTo = true;
            if (parameter is bool p)
            {
                compareTo = p;
            }
            else if (parameter is string str && str.ToUpper().Equals("FALSE"))
            {
                compareTo = false;
            }
            return ((bool)value) == compareTo ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
