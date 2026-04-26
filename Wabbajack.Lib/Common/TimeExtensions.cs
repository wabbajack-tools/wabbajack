using System;

namespace Wabbajack.Common;

public static class TimeExtensions
{
    public static DateTime AsUnixTime(this long timestamp)
    {
        var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        dtDateTime = dtDateTime.AddSeconds(timestamp);
        return dtDateTime;
    }

    public static ulong AsUnixTime(this DateTime timestamp)
    {
        var diff = timestamp - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        return (ulong) diff.TotalSeconds;
    }
}