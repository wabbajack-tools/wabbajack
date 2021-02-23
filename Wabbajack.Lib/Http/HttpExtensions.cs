using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace Wabbajack.Lib.Http
{
    public static class HttpExtensions
    {
        public static IEnumerable<(string Key, string Value)> GetSetCookies(this HttpResponseMessage response)
        {
            if (!response.Headers.TryGetValues("set-cookie", out var values))
                return Array.Empty<(string, string)>();
            
            return values
                .SelectMany(h => h.Split(";"))
                .Select(h => h.Split("="))
                .Where(h => h.Length == 2)
                .Select(h => (h[0], h[1]));
        }
    }
}
