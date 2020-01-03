using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading.Tasks;
using CefSharp;
using Wabbajack.Lib.LibCefHelpers;

namespace Wabbajack.Lib.WebAutomation
{
    public class CefSharpWrapper : IWebDriver
    {
        private IWebBrowser _browser;

        public CefSharpWrapper(IWebBrowser browser)
        {
            _browser = browser;
        }

        public Task NavigateTo(Uri uri)
        {
            var tcs = new TaskCompletionSource<bool>();

            EventHandler<LoadingStateChangedEventArgs> handler = null;
            handler = (sender, e) =>
            {
                if (!e.IsLoading)
                {
                    _browser.LoadingStateChanged -= handler;
                    tcs.SetResult(true);
                }
            };

            _browser.LoadingStateChanged += handler;
            _browser.Load(uri.ToString());
            return tcs.Task;
        }

        public async Task<string> EvaluateJavaScript(string text)
        {
            var result = await _browser.EvaluateScriptAsync(text);
            if (!result.Success)
                throw new Exception(result.Message);

            return (string)result.Result;
        }

        public Task<Helpers.Cookie[]> GetCookies(string domainPrefix)
        {
            return Helpers.GetCookies(domainPrefix);
        }

        public async Task WaitForInitialized()
        {
            while (!_browser.IsBrowserInitialized)
                await Task.Delay(100);
        }
    }
}
