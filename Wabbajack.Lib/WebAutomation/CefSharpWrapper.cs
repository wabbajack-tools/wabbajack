using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CefSharp;
using Wabbajack.Common;
using Wabbajack.Lib.LibCefHelpers;

namespace Wabbajack.Lib.WebAutomation
{
    public class CefSharpWrapper : IWebDriver
    {
        private readonly IWebBrowser _browser;
        public Action<Uri>? DownloadHandler { get; set; }
        public CefSharpWrapper(IWebBrowser browser)
        {
            _browser = browser;

            _browser.DownloadHandler = new DownloadHandler(this);
            _browser.LifeSpanHandler = new PopupBlocker(this);
        }

        public Task NavigateTo(Uri uri)
        {
            var tcs = new TaskCompletionSource<bool>();

            EventHandler<LoadingStateChangedEventArgs>? handler = null;
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

        public async Task<long> NavigateToAndDownload(Uri uri, AbsolutePath dest, bool quickMode = false)
        {
            var oldCB = _browser.DownloadHandler;

            var handler = new ReroutingDownloadHandler(this, dest, quickMode: quickMode);
            _browser.DownloadHandler = handler;

            try
            {
                await NavigateTo(uri);
                return await handler.Task;
            }
            finally {
                _browser.DownloadHandler = oldCB;
           
            }
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

        public string Location => _browser.Address;
    }

    public class PopupBlocker : ILifeSpanHandler
    {
        private readonly CefSharpWrapper _wrapper;

        public PopupBlocker(CefSharpWrapper cefSharpWrapper)
        {
            _wrapper = cefSharpWrapper;
        }

        public bool OnBeforePopup(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl,
            string targetFrameName, WindowOpenDisposition targetDisposition, bool userGesture, IPopupFeatures popupFeatures,
            IWindowInfo windowInfo, IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser? newBrowser)
        {
            // Block popups
            newBrowser = null;
            return true;
        }

        public void OnAfterCreated(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
        }

        public bool DoClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            return false;
        }

        public void OnBeforeClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
        }
    }

    public class ReroutingDownloadHandler : IDownloadHandler
    {
        private CefSharpWrapper _wrapper;
        private AbsolutePath _path;
        public TaskCompletionSource<long> _tcs = new TaskCompletionSource<long>();
        private bool _quickMode;
        public Task<long> Task => _tcs.Task;

        public ReroutingDownloadHandler(CefSharpWrapper wrapper, AbsolutePath path, bool quickMode)
        {
            _wrapper = wrapper;
            _path = path;
            _quickMode = quickMode;
        }

        public void OnBeforeDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem,
            IBeforeDownloadCallback callback)
        {
            if (_quickMode) return;
            callback.Continue(_path.ToString(), false);
        }

        public void OnDownloadUpdated(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem,
            IDownloadItemCallback callback)
        {
            if (_quickMode)
            {
                callback.Cancel();
                _tcs.SetResult(downloadItem.TotalBytes);
                return;
            }
            
            if (downloadItem.IsComplete)
                _tcs.SetResult(downloadItem.TotalBytes);
            callback.Resume();
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
            _wrapper.DownloadHandler?.Invoke(new Uri(downloadItem.Url));
        }

        public void OnDownloadUpdated(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem,
            IDownloadItemCallback callback)
        {
            callback.Cancel();
        }
    }
}
