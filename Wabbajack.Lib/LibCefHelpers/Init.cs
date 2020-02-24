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

namespace Wabbajack.Lib.LibCefHelpers
{
    public static class Helpers
    {
        public static Common.Http.Client GetClient(IEnumerable<Cookie> cookies, string referer)
        {
            var client = new Common.Http.Client();
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

        public class Cookie
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public string Domain { get; set; }
            public string Path { get; set; }
        }

        public static void Init()
        {
            if (Inited) return;
            Inited = true;
            CefSettings settings = new CefSettings();
            settings.CachePath = Path.Combine(Consts.LocalAppDataPath  + @"\CEF");
            Cef.Initialize(settings);
        }

        public static bool Inited { get; set; }
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
