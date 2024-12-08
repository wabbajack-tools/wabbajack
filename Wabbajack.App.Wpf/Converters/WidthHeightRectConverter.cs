using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Wabbajack
{
    public class WidthHeightRectConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            double rectWidth = 0;
            double rectHeight = 0;
            if (values[0] is not null && double.TryParse(values[0].ToString(), out var width))
                rectWidth = width;
            else return null;
            if (values[1] is not null && double.TryParse(values[1].ToString(), out var height))
                rectHeight = height;
            else return null;
            return new Rect(0, 0, rectWidth, rectHeight);
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
