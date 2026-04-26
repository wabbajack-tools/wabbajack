using System;

namespace Wabbajack.Common;

public static class DateTimeExtensions
{
    public static DateTime TruncateToDate(this DateTime d)
    {
        return new DateTime(d.Year, d.Month, d.Day);
    }
    
}