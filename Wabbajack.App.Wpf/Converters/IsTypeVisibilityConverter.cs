using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Wabbajack
{
    public class IsTypeVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType != typeof(Visibility))
                throw new InvalidOperationException($"The target must be of type {nameof(Visibility)}");

            if (!(parameter is Type paramType))
            {
                throw new ArgumentException();
            }
            if (value == null) return Visibility.Collapsed;
            return paramType.Equals(value.GetType()) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
