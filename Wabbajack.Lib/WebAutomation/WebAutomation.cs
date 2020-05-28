using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CefSharp;
using CefSharp.OffScreen;

namespace Wabbajack.Lib.WebAutomation
{
    public class Driver : IDisposable
    {
        private IWebBrowser _browser;
        private CefSharpWrapper _driver;

        public Driver()
        {

            _browser = new ChromiumWebBrowser();

            _driver = new CefSharpWrapper(_browser);
        }
        public static async Task<Driver> Create()
        {
            var driver = new Driver();
            await driver._driver.WaitForInitialized();
            return driver;
        }

        public async Task<Uri?> NavigateTo(Uri uri)
        {
            await _driver.NavigateTo(uri);
            return await GetLocation();
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
    }
}
