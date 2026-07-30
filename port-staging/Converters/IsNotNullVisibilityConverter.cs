using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Wabbajack
{
    /// <summary>
    /// Evaluates any object and converts it to a bool (bound to IsVisible) based on if it is null.
    /// By default it will show if the object is not null, and hide when it is null.
    /// If ConverterParameter is set to false, then this behavior is inverted
    /// </summary>
    public class IsNotNullVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool compareTo = true;
            if (parameter is bool p)
            {
                compareTo = p;
            }
            else if (parameter is string str && str.ToUpper().Equals("FALSE"))
            {
                compareTo = false;
            }
            bool isNull = value != null;
            return isNull == compareTo;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
