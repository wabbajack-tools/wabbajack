using System;

namespace Wabbajack
{
    public static class StringExt
    {
        public static bool ContainsCaseInsensitive(this string str, string value)
        {
            return str.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
