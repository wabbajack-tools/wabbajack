using System;
using CefNet;
using CefNet.Avalonia;
using CefNet.Internal;
using Wabbajack.Paths;

namespace Wabbajack.CLI.Browser;

public class CustomWebView : WebView
{
    public CustomGlue Glue { get; private set; }

    protected override WebViewGlue CreateWebViewGlue()
    {
        Glue = new CustomGlue(this);
        return Glue;
    }
}

public class CustomGlue : AvaloniaWebViewGlue
{
    private readonly CustomWebView _view;
    public Func<Uri, AbsolutePath>? RedirectDownloadsFn { get; set; }

    public CustomGlue(CustomWebView view) : base(view)
    {
        _view = view;
    }

    protected override void OnBeforeDownload(CefBrowser browser, CefDownloadItem downloadItem, string suggestedName,
        CefBeforeDownloadCallback callback)
    {
        if (RedirectDownloadsFn == null)
        {
            base.OnBeforeDownload(browser, downloadItem, suggestedName, callback);
            return;
        }
        
        var path = RedirectDownloadsFn!(new Uri(downloadItem.OriginalUrl));
        callback.Continue(path.ToString(), false);
    }

    protected override void OnDownloadUpdated(CefBrowser browser, CefDownloadItem downloadItem, CefDownloadItemCallback callback)
    {
        base.OnDownloadUpdated(browser, downloadItem, callback);
    }
}