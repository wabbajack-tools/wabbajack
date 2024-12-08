using System;
using System.Globalization;
using System.Windows.Data;
using ReactiveUI;
using Wabbajack.Paths;

namespace Wabbajack
{
    public class AbsolutePathToStringConverter : IBindingTypeConverter, IValueConverter
    {
        public int GetAffinityForObjects(Type fromType, Type toType)
        {
            if (toType == typeof(object)) return 1;
            if (toType == typeof(string)) return 1;
            if (toType == typeof(AbsolutePath)) return 1;
            if (toType == typeof(AbsolutePath?)) return 1;
            return 0;
        }

        public bool TryConvert(object @from, Type toType, object conversionHint, out object result)
        {
            if (toType == typeof(AbsolutePath))
            {
                if (@from is string s)
                {
                    try
                    {
                        result = (AbsolutePath)s;
                        return true;
                    }
                    catch
                    {
                        result = (AbsolutePath)"";
                        return false;
                    }
                }

                if (@from is AbsolutePath abs)
                {
                    result = abs;
                    return true;
                }
            }
            else if (toType == typeof(string))
            {
                if (@from is string s)
                {
                    result = default;
                    return false;
                }

                if (@from is AbsolutePath abs)
                {
                    result = abs.ToString();
                    return true;
                }
            }

            result = default;
            return false;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType != typeof(string))
                throw new InvalidOperationException($"The target must be of type string");
            if (value is AbsolutePath path)
            {
                return path.ToString();
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return AbsolutePath.ConvertNoFailure((string) value);
        }
    }
}
