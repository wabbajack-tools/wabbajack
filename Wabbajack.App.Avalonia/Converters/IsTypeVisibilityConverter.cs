using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Wabbajack
{
    public class IsTypeVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType != typeof(bool))
                throw new InvalidOperationException($"The target must be of type {nameof(Boolean)}");

            if (!(parameter is Type paramType))
            {
                throw new ArgumentException();
            }
            if (value == null) return false;
            return paramType.Equals(value.GetType());
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
