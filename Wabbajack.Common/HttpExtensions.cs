using System.Linq;
using System.Net.Http;
using Wabbajack.DTOs.Logins;

namespace Wabbajack.Common
{
    public static class HttpExtensions
    {
        public static HttpRequestMessage AddCookies(this HttpRequestMessage msg, Cookie[] cookies)
        {
            msg.Headers.Add("Cookie", string.Join(";", cookies.Select(c => $"{c.Name}={c.Value}")));
            return msg;
        }

        public static HttpRequestMessage AddChromeAgent(this HttpRequestMessage msg)
        {
            msg.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/92.0.4515.159 Safari/537.36");
            return msg;
        }

    }
}