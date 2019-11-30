using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Wabbajack
{
    /// <summary>
    /// Evaluates any object and converts it to a visibility based on if it is null.
    /// By default it will show if the object is not null, and collapse when it is null.
    /// If ConverterParameter is set to false, then this behavior is inverted
    /// </summary>
    public class IsNotNullVisibilityConverter : IValueConverter
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
            bool isNull = value != null;
            return isNull == compareTo ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
