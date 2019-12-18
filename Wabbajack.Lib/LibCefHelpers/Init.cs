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
using Wabbajack.Common;
using Xilium.CefGlue;

namespace Wabbajack.Lib.LibCefHelpers
{
    public static class Helpers
    {

        /// <summary>
        /// We bundle the cef libs inside the .exe, we need to extract them before loading any wpf code that requires them
        /// </summary>
        public static async Task ExtractLibs()
        {
            if (File.Exists("cefglue.7z") && File.Exists("libcef.dll")) return;

            using (var fs = File.OpenWrite("cefglue.7z"))
            using (var rs = Assembly.GetExecutingAssembly().GetManifestResourceStream("Wabbajack.Lib.LibCefHelpers.cefglue.7z"))
            {
                rs.CopyTo(fs);
                Utils.Log("Extracting libCef files");
            }
            using (var wq = new WorkQueue(1))
            {
                await FileExtractor.ExtractAll(wq, "cefglue.7z", ".");
            }
        }
        public static HttpClient GetClient(IEnumerable<Cookie> cookies, string referer)
        {
            var container = ToCookieContainer(cookies);
            var handler = new HttpClientHandler { CookieContainer = container };
            var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Referrer = new Uri(referer);
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

        public static async Task<Cookie[]> GetCookies(string domainEnding)
        {
            var manager = CefCookieManager.GetGlobal(null);
            var visitor = new CookieVisitor();
            if (!manager.VisitAllCookies(visitor))
                return new Cookie[0];
            var cc = await visitor.Task;

            return (await visitor.Task).Where(c => c.Domain.EndsWith(domainEnding)).ToArray();
        }


        private class CookieVisitor : CefCookieVisitor
        {
            TaskCompletionSource<List<Cookie>> _source = new TaskCompletionSource<List<Cookie>>();
            public Task<List<Cookie>> Task => _source.Task;

            public List<Cookie> Cookies { get; } = new List<Cookie>();
            protected override bool Visit(CefCookie cookie, int count, int total, out bool delete)
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
                delete = false;
                return true;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    _source.SetResult(Cookies);
            }


        }

        public class Cookie
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public string Domain { get; set; }
            public string Path { get; set; }
        }
    }
}
