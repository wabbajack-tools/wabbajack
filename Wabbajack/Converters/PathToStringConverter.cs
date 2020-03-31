using System;
using ReactiveUI;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;

namespace Wabbajack.Converters
{
    public class PathToStringConverter : IBindingTypeConverter
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
                    result = (string)abs;
                    return true;
                }
            }

            result = default;
            return false;
        }
    }
}
