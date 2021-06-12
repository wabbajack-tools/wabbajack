using System;
using System.Threading;
using System.Threading.Tasks;
using CefSharp;
using Wabbajack.Common;
using Wabbajack.Common.Exceptions;
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

        public Task NavigateTo(Uri uri, CancellationToken? token = null)
        {
            var tcs = new TaskCompletionSource<bool>();

            EventHandler<LoadingStateChangedEventArgs>? handler = null;
            handler = (sender, e) =>
            {
                if (e.IsLoading) return;

                _browser.LoadingStateChanged -= handler;
                tcs.SetResult(true);
            };
            _browser.LoadingStateChanged += handler;
            _browser.Load(uri.ToString());
            token?.Register(() => tcs.TrySetCanceled());
            
            return tcs.Task;
        }

        private readonly string[] KnownServerLoadStrings =
        {
            "<h1>Temporarily Unavailable</h1>",
            "<center>Request Header Or Cookie Too Large</center>",
            //"<html><head></head><body></body></html>"
            "<span class=\"cf-error-code\">525</span>",
            "<span class=\"cf-error-code\">522</span>",
        };
        private readonly (string, int)[] KnownErrorStrings =
        {
            ("<h1>400 Bad Request</h1>", 400),
            ("We could not locate the item you are trying to view.", 404),
        };
        private static readonly Random RetryRandom = new Random();

        public async Task<long> NavigateToAndDownload(Uri uri, AbsolutePath dest, bool quickMode = false, CancellationToken? token = null)
        {
            var oldCB = _browser.DownloadHandler;

            var handler = new ReroutingDownloadHandler(this, dest, quickMode: quickMode, token);
            _browser.DownloadHandler = handler;

            try
            {
                int retryCount = 0;
                RETRY:
                await NavigateTo(uri, token);
                var source = await _browser.GetSourceAsync();
                foreach (var err in KnownServerLoadStrings)
                {
                    if (!source.Contains(err))
                        continue;

                    if ((token?.IsCancellationRequested) == true)
                    {
                        throw new TimeoutException();
                    }
                    else
                    {
                        retryCount += 1;
                        var retry = RetryRandom.Next(retryCount * 5000, retryCount * 5000 * 2);
                        Utils.Log($"Got server load error from {uri} retying in {retry}ms [{err}]");
                        await Task.Delay(TimeSpan.FromMilliseconds(retry));
                        goto RETRY;
                    }
                }

                foreach (var (err, httpCode) in KnownErrorStrings)
                {
                    if (source.Contains(err))
                        throw new HttpException(httpCode,$"Web driver failed: {err}");
                }

                Utils.Log($"Loaded page {uri} starting download...");
                return await handler.TaskResult;
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
        private CancellationToken? _cancelationToken;
        public Task<long> TaskResult => _tcs.Task;

        public ReroutingDownloadHandler(CefSharpWrapper wrapper, AbsolutePath path, bool quickMode, CancellationToken? token)
        {
            _wrapper = wrapper;
            _path = path;
            _quickMode = quickMode;
            _cancelationToken = token;
            token?.Register(() => _tcs.TrySetCanceled());
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
                _tcs.TrySetResult(downloadItem.TotalBytes);
                return;
            }
            
            if (downloadItem.IsComplete)
                _tcs.TrySetResult(downloadItem.TotalBytes);
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
