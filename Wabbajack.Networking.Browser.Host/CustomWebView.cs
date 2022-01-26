using System;
using CefNet;
using CefNet.Avalonia;
using CefNet.Internal;
using Wabbajack.Paths;

namespace Wabbajack.Networking.Browser;

public class CustomWebView : WebView
{
    private CustomGlue Glue { get; }

    public CustomWebView() : base()
    {
        Glue = new CustomGlue(this);
    }

    protected override WebViewGlue CreateWebViewGlue()
    {
        return Glue;
    }
}

class CustomGlue : AvaloniaWebViewGlue
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
        downloadItem.
        base.OnDownloadUpdated(browser, downloadItem, callback);
    }
}