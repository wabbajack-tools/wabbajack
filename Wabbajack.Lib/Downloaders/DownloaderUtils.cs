using System;

namespace Wabbajack.Lib.Downloaders
{
    public static class DownloaderUtils
    {
        public static Uri GetDirectURL(dynamic meta)
        {
            var url = meta?.General?.directURL;
            if (url == null) return null;
            
            return Uri.TryCreate((string) url, UriKind.Absolute, out var result) ? result : null;
        }
    }
}
