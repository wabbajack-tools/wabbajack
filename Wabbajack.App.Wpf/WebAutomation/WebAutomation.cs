using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CefSharp;
using CefSharp.OffScreen;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.LibCefHelpers;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.WebAutomation
{
    public class Driver : IDisposable
    {
        private readonly IWebBrowser _browser;
        private readonly CefSharpWrapper _driver;

        public Driver(ILogger logger)
        {

            _browser = new ChromiumWebBrowser();
            _driver = new CefSharpWrapper(logger, _browser);
        }
        public static async Task<Driver> Create(ILogger logger)
        {
            var driver = new Driver(logger);
            await driver._driver.WaitForInitialized();
            return driver;
        }

        public async Task<Uri?> NavigateTo(Uri uri, CancellationToken? token = null)
        {
            try
            {
                await _driver.NavigateTo(uri, token);
                return await GetLocation();
            }
            catch (TaskCanceledException ex)
            {
                await DumpState(uri, ex);
                throw;
            }
        }

        private async Task DumpState(Uri uri, Exception ex)
        {
            var file = KnownFolders.EntryPoint.Combine("CEFStates", DateTime.UtcNow.ToFileTimeUtc().ToString())
                .WithExtension(new Extension(".html"));
            file.Parent.CreateDirectory();
            var source = await GetSourceAsync();
            var cookies = await Helpers.GetCookies();
            var cookiesString = string.Join('\n', cookies.Select(c => c.Name + " - " + c.Value));
            await file.WriteAllTextAsync(uri + "\n " + source + "\n" + ex + "\n" + cookiesString);
        }

        public async Task<long> NavigateToAndDownload(Uri uri, AbsolutePath absolutePath, bool quickMode = false, CancellationToken? token = null)
        {
            try
            {
                return await _driver.NavigateToAndDownload(uri, absolutePath, quickMode: quickMode, token: token);
            }
            catch (TaskCanceledException ex) {
                await DumpState(uri, ex);
                throw;
            }
        }

        public async ValueTask<Uri?> GetLocation()
        {
            try
            {
                return new Uri(_browser.Address);
            }
            catch (UriFormatException)
            {
                return null;
            }
        }

        public async ValueTask<string> GetSourceAsync()
        {
            return await _browser.GetSourceAsync();
        }
        
        public async ValueTask<HtmlDocument> GetHtmlAsync()
        {
            var body = await GetSourceAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(body);
            return doc;
        }

        public Action<Uri?> DownloadHandler { 
            set => _driver.DownloadHandler = value;
        }

        public Task<string> GetAttr(string selector, string attr)
        {
            return _driver.EvaluateJavaScript($"document.querySelector(\"{selector}\").{attr}");
        }

        public Task<string> EvalJavascript(string js)
        {
            return _driver.EvaluateJavaScript(js);
        }

        public void Dispose()
        {
            _browser.Dispose();
        }

        public static void ClearCache()
        {
            Helpers.ClearCookies();
        }

        public async Task DeleteCookiesWhere(Func<Helpers.Cookie, bool> filter)
        {
            await Helpers.DeleteCookiesWhere(filter);
        }
    }
}
