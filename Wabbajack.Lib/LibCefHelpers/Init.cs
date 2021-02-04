using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using CefSharp;
using CefSharp.OffScreen;
using Wabbajack.Common;
using Wabbajack.Common.Serialization.Json;
using Cookie = CefSharp.Cookie;

namespace Wabbajack.Lib.LibCefHelpers
{
    public static class Helpers
    {
        public static Wabbajack.Lib.Http.Client GetClient(IEnumerable<Cookie> cookies, string referer)
        {
            var client = new Wabbajack.Lib.Http.Client();
            client.Headers.Add(("Referrer", referer));
            client.Cookies.AddRange(cookies.Select(cookie => new System.Net.Cookie(cookie.Name, cookie.Value, cookie.Path, cookie.Domain)));
            return client;
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

        public static async Task<Cookie[]> GetCookies(string domainEnding = "")
        {
            var manager = Cef.GetGlobalCookieManager();
            var visitor = new CookieVisitor();
            if (!manager.VisitAllCookies(visitor))
                return new Cookie[0];
            var cc = await visitor.Task;

            return (await visitor.Task).Where(c => c.Domain.EndsWith(domainEnding)).ToArray();
        }

        private class CookieVisitor : ICookieVisitor
        {
            TaskCompletionSource<List<Cookie>> _source = new TaskCompletionSource<List<Cookie>>();
            public Task<List<Cookie>> Task => _source.Task;

            public List<Cookie> Cookies { get; } = new List<Cookie>();
            public void Dispose()
            {
                _source.SetResult(Cookies);
            }

            public bool Visit(CefSharp.Cookie cookie, int count, int total, ref bool deleteCookie)
            {
                Cookies.Add(new Cookie
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

        [JsonName("HttpCookie")]
        public class Cookie
        {
            public string Name { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
            public string Domain { get; set; } = string.Empty;
            public string Path { get; set; } = string.Empty;
        }

        public static void Init()
        {
            if (Inited || Cef.IsInitialized) return;
            Inited = true;
            CefSettings settings = new CefSettings();
            settings.CachePath = Consts.CefCacheLocation.ToString();
            settings.JavascriptFlags = "--noexpose_wasm";
            Cef.Initialize(settings);
        }

        public static bool Inited { get; set; }

        public static void ClearCookies()
        {
            var manager = Cef.GetGlobalCookieManager();
            var visitor = new CookieDeleter();
            manager.VisitAllCookies(visitor);
        }

        public static async Task DeleteCookiesWhere(Func<Cookie,bool> filter)
        {
            var manager = Cef.GetGlobalCookieManager();
            var visitor = new CookieDeleter(filter);
            manager.VisitAllCookies(visitor);
        }
    }

    class CookieDeleter : ICookieVisitor
    {
        private Func<Helpers.Cookie, bool>? _filter;

        public CookieDeleter(Func<Helpers.Cookie, bool>? filter = null)
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
                var conv = new Helpers.Cookie
                {
                    Name = cookie.Name, Domain = cookie.Domain, Value = cookie.Value, Path = cookie.Path
                };
                if (_filter(conv))
                    deleteCookie = true;
            }

            return true;
        }
    }

    public static class ModuleInitializer
    {
        public static void Initialize()
        {
            var es = Assembly.GetEntryAssembly();
            if (es != null && es.Location != null && Path.GetFileNameWithoutExtension(es.Location) == "Wabbajack") 
                Helpers.Init();
        }
    }
}
