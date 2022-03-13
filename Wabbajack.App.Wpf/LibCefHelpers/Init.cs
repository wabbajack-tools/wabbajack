using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using CefSharp;
using CefSharp.OffScreen;
using Wabbajack.Common;
using Wabbajack.DTOs.JsonConverters;
using Cookie = CefSharp.Cookie;

namespace Wabbajack.LibCefHelpers
{
    public static class Helpers
    {
        public static HttpRequestMessage MakeMessage(HttpMethod method, Uri uri, IEnumerable<Cookie> cookies, string referer)
        {
            var msg = new HttpRequestMessage(method, uri);
            msg.Headers.Add("Referrer", referer);
            var cs = string.Join(",", cookies.Select(c => $"{c.Name}={c.Value}"));
            msg.Headers.Add("Cookie", cs);
            return msg;
        }

        private static CookieContainer ToCookieContainer(IEnumerable<Cookie> cookies)
        {
            var container = new CookieContainer();
            cookies
                .Do(cookie =>
                {
                    container.Add(new System.Net.Cookie(cookie.Name, cookie.Value, cookie.Path, cookie.Domain));
                });

            return container;
        }

        public static async Task<DTOs.Logins.Cookie[]> GetCookies(string domainEnding = "")
        {
            var manager = Cef.GetGlobalCookieManager();
            var visitor = new CookieVisitor();
            if (!manager.VisitAllCookies(visitor))
                return Array.Empty<DTOs.Logins.Cookie>();
            var cc = await visitor.Task;

            return (await visitor.Task).Where(c => c.Domain.EndsWith(domainEnding)).ToArray();
        }

        private class CookieVisitor : ICookieVisitor
        {
            TaskCompletionSource<List<DTOs.Logins.Cookie>> _source = new();
            public Task<List<DTOs.Logins.Cookie>> Task => _source.Task;

            public List<DTOs.Logins.Cookie> Cookies { get; } = new ();
            public void Dispose()
            {
                _source.SetResult(Cookies);
            }

            public bool Visit(CefSharp.Cookie cookie, int count, int total, ref bool deleteCookie)
            {
                Cookies.Add(new DTOs.Logins.Cookie
                {
                    Name = cookie.Name,
                    Value = cookie.Value,
                    Domain = cookie.Domain,
                    Path = cookie.Path
                });
                if (count == total)
                    _source.SetResult(Cookies);
                deleteCookie = false;
                return true;
            }
        }
        public static void ClearCookies()
        {
            var manager = Cef.GetGlobalCookieManager();
            var visitor = new CookieDeleter();
            manager.VisitAllCookies(visitor);
        }

        public static async Task DeleteCookiesWhere(Func<DTOs.Logins.Cookie,bool> filter)
        {
            var manager = Cef.GetGlobalCookieManager();
            var visitor = new CookieDeleter(filter);
            manager.VisitAllCookies(visitor);
        }
    }

    class CookieDeleter : ICookieVisitor
    {
        private Func<DTOs.Logins.Cookie, bool>? _filter;

        public CookieDeleter(Func<DTOs.Logins.Cookie, bool>? filter = null)
        {
            _filter = filter;
        }
        public void Dispose()
        {
        }

        public bool Visit(Cookie cookie, int count, int total, ref bool deleteCookie)
        {
            if (_filter == null)
            {
                deleteCookie = true;
            }
            else
            {
                var conv = new DTOs.Logins.Cookie
                {
                    Name = cookie.Name, Domain = cookie.Domain, Value = cookie.Value, Path = cookie.Path
                };
                if (_filter(conv))
                    deleteCookie = true;
            }

            return true;
        }
    }
}
