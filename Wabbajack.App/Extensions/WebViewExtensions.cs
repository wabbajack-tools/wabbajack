using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CefNet;
using CefNet.Avalonia;
using HtmlAgilityPack;
using Wabbajack.DTOs.Logins;

namespace Wabbajack.App.Extensions;

public static class WebViewExtensions
{
    public static async Task WaitForReady(this WebView view)
    {
        while (view.BrowserObject == null) await Task.Delay(200);
    }

    /// <summary>
    ///     Navigates to the URL and waits until the page is finished loading
    /// </summary>
    /// <param name="view"></param>
    /// <param name="uri"></param>
    public static async Task NavigateTo(this WebView view, Uri uri)
    {
        view.Navigate(uri.ToString());
        while (view.IsBusy) await Task.Delay(200);
    }

    public static async Task<Cookie[]> Cookies(this WebView view, string domainEnding, CancellationToken token)
    {
        var results = CefCookieManager.GetGlobalManager(null)!;
        var cookies = await results.GetCookiesAsync(c => c.Domain.EndsWith(domainEnding), token)!;
        return cookies.Select(c => new Cookie
        {
            Domain = c.Domain,
            Name = c.Name,
            Path = c.Path,
            Value = c.Value
        }).ToArray();
    }

    public static async Task EvaluateJavaScript(this WebView view, string js)
    {
        view.GetMainFrame().ExecuteJavaScript(js, "", 0);
    }

    public static async Task<HtmlDocument> GetDom(this WebView view, CancellationToken token)
    {
        var source = await view.GetMainFrame().GetSourceAsync(token);
        var doc = new HtmlDocument();
        doc.LoadHtml(source);
        return doc;
    }
}