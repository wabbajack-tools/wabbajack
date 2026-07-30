using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Wabbajack
{
    // NOTE: Ported from WPF's BoolToVisibilityConverter (Visibility -> bool).
    // Avalonia has no Visibility type; controls use the bool IsVisible property directly,
    // so this converter now produces/consumes bool instead of Visibility.
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType != typeof(bool))
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

            return b == compareTo;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
