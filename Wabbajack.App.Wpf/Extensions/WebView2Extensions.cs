using System;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Wabbajack.App.Wpf.Services;

namespace Wabbajack.App.Wpf.Extensions;

public static class WebView2Extensions
{
    public static async Task InitializeAdBlocking(this WebView2 browser, AdBlockService adBlockService)
    {
        while (browser.CoreWebView2 == null)
        {
            await Task.Delay(250);
        }

        browser.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        browser.CoreWebView2.WebResourceRequested += (sender, args) =>
        {
            if (adBlockService.IsBlocked(new Uri(args.Request.Uri)))
            {
                args.Response = browser.CoreWebView2.Environment.CreateWebResourceResponse(null, 403, "Forbidden", "");
            }
        };
    }
}
