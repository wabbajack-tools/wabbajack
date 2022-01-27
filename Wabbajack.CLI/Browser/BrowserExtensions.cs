using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CefNet;
using CefNet.Avalonia;
using HtmlAgilityPack;
using Wabbajack.DTOs.Logins;

namespace Wabbajack.CLI.Browser;

public static class BrowserExtensions
{
    public static async Task WaitForReady(this WebView browser)
    {
        while (!browser.IsInitialized)
        {
            await Task.Delay(250);
        }
        
        while (browser.BrowserObject == null)
        {
            await Task.Delay(250);
        }
    }

    public static async Task NavigateTo(this WebView browser, Uri location)
    {
        browser.Navigate(location.ToString());
        await browser.WaitForIdle();
    }

    public static async Task WaitForIdle(this WebView browser)
    {
        while (browser.IsBusy)
        {
            await Task.Delay(250);
        }
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