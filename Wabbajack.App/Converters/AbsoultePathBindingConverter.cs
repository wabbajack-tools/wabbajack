using System;
using ReactiveUI;
using Wabbajack.Paths;

namespace Wabbajack.App.Converters
{
    public class AbsoultePathBindingConverter : IBindingTypeConverter
    {
        public int GetAffinityForObjects(Type fromType, Type toType)
        {
            if (fromType == typeof(string) && toType == typeof(AbsolutePath) ||
                fromType == typeof(AbsolutePath) && toType == typeof(string))
                return 100;
            return 0;
        }

        public bool TryConvert(object? @from, Type toType, object? conversionHint, out object? result)
        {
            switch (@from)
            {
                case string s:
                    result = (AbsolutePath)s;
                    return true;
                case AbsolutePath ap:
                    result = ap.ToString();
                    return true;
                default:
                    result = null;
                    return false;
            }
        }
    }
}