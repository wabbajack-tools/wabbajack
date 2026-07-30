using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Wabbajack
{
    public class BoolToVisibilityHiddenConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType != typeof(bool) && targetType != typeof(object))
                throw new InvalidOperationException($"The target must be of type {nameof(Boolean)}");
            bool compareTo = true;
            if (parameter is bool p)
            {
                compareTo = p;
            }
            else if (parameter is string str && str.ToUpper().Equals("FALSE"))
            {
                compareTo = false;
            }
            if (value is not bool b)
                return AvaloniaProperty.UnsetValue;
            // WPF: Visibility.Collapsed when equal, Visibility.Visible otherwise.
            // IsVisible=false when equal (hidden), true otherwise.
            return b == compareTo ? false : true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
