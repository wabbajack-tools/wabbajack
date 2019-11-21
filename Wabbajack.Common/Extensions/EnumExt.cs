using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Wabbajack.Common
{
    public static class EnumExt
    {
        public static IEnumerable<T> GetValues<T>()
            where T : struct, Enum
        {
            return Enum.GetValues(typeof(T)).Cast<T>();
        }

        public static string ToDescriptionString<TEnum>(this TEnum val)
            where TEnum : struct, IConvertible
        {
            if (!typeof(TEnum).IsEnum)
            {
                throw new ArgumentException("T must be an Enum");
            }

            var attributes = (DescriptionAttribute[])val.GetType().GetField(val.ToString()).GetCustomAttributes(typeof(DescriptionAttribute), false);
            return attributes.Length > 0 ? attributes[0].Description : val.ToString();
        }
    }
}
