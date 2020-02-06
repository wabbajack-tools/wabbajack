using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using CefSharp;
using Wabbajack.Common;
using Wabbajack.Lib.LibCefHelpers;

namespace Wabbajack.Lib.WebAutomation
{
    public class CefSharpWrapper : IWebDriver
    {
        private IWebBrowser _browser;
        public Action<Uri> DownloadHandler { get; set; }
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
            _browser.DownloadHandler = new DownloadHandler(this);
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

        private const string CefStateName = "cef-state";

        public async Task WaitForInitialized()
        {
            while (!_browser.IsBrowserInitialized)
                await Task.Delay(100);
        }
    }

    public class DownloadHandler : IDownloadHandler
    {
        private CefSharpWrapper _wrapper;

        public DownloadHandler(CefSharpWrapper wrapper)
        {
            _wrapper = wrapper;
        }

        public void OnBeforeDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem,
            IBeforeDownloadCallback callback)
        {
            _wrapper.DownloadHandler(new Uri(downloadItem.Url));
        }

        public void OnDownloadUpdated(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem,
            IDownloadItemCallback callback)
        {
            callback.Cancel();
        }
    }
}
