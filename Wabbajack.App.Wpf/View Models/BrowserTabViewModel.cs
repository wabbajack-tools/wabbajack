using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.DTOs.Interventions;
using Wabbajack.DTOs.Logins;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Messages;
using Wabbajack.Paths;
using Wabbajack.Views;

namespace Wabbajack;

public abstract class BrowserTabViewModel : ViewModel
{
    [Reactive] public string HeaderText { get; set; }

    [Reactive] public string Instructions { get; set; }

    public BrowserView? Browser { get; set; }

    private WebView2 _browser => Browser!.Browser;

    public async Task RunWrapper(CancellationToken token)
    {
        await Run(token);
        MessageBus.Current.SendMessage(new CloseBrowserTab(this));
    }

    protected abstract Task Run(CancellationToken token);

    protected async Task WaitForReady()
    {
        while (Browser?.Browser.CoreWebView2 == null)
        {
            await Task.Delay(250);
        }
    }

    public async Task NavigateTo(Uri uri)
    {
        var tcs = new TaskCompletionSource();

        void Completed(object? o, CoreWebView2NavigationCompletedEventArgs a)
        {
            if (a.IsSuccess)
            {
                tcs.TrySetResult();
            }
            else
            {
                tcs.TrySetException(new Exception($"Navigation error to {uri}"));
            }
        }

        _browser.NavigationCompleted += Completed;
        _browser.Source = uri;
        await tcs.Task;
        _browser.NavigationCompleted -= Completed;
    }

    public async Task<Cookie[]> GetCookies(string domainEnding, CancellationToken token)
    {
        var cookies = (await _browser.CoreWebView2.CookieManager.GetCookiesAsync(""))
            .Where(c => c.Domain.EndsWith(domainEnding));
        return cookies.Select(c => new Cookie
        {
            Domain = c.Domain,
            Name = c.Name,
            Path = c.Path,
            Value = c.Value
        }).ToArray();
    }

    public async Task<string> EvaluateJavaScript(string js)
    {
        return await _browser.ExecuteScriptAsync(js);
    }

    public async Task<HtmlDocument> GetDom(CancellationToken token)
    {
        var source = await EvaluateJavaScript("document.body.outerHTML");
        var decoded = JsonSerializer.Deserialize<string>(source);
        var doc = new HtmlDocument();
        doc.LoadHtml(decoded);
        return doc;
    }

    public async Task<ManualDownload.BrowserDownloadState> WaitForDownloadUri(CancellationToken token)
    {
        var source = new TaskCompletionSource<Uri>();
        var referer = _browser.Source;
        _browser.CoreWebView2.DownloadStarting += (sender, args) =>
        {
            try
            {
                
                source.SetResult(new Uri(args.DownloadOperation.Uri));
            }
            catch (Exception ex)
            {
                source.SetCanceled();
            }

            args.Handled = true;
            args.Cancel = true;
        };

        var uri = await source.Task.WaitAsync(token);
        var cookies = await GetCookies(uri.Host, token);
        return new ManualDownload.BrowserDownloadState(uri, cookies, new[]
        {
            ("Referer", referer.ToString())
        });
    }
    
    public async Task<Hash> WaitForDownload(AbsolutePath path, CancellationToken token)
    {
        var source = new TaskCompletionSource();
        var referer = _browser.Source;
        _browser.CoreWebView2.DownloadStarting += (sender, args) =>
        {
            try
            {
                args.ResultFilePath = path.ToString();
                args.Handled = true;
                args.DownloadOperation.StateChanged += (o, o1) =>
                {
                    var operation = (CoreWebView2DownloadOperation) o;
                    if (operation.State == CoreWebView2DownloadState.Completed)
                        source.TrySetResult();
                };
            }
            catch (Exception ex)
            {
                source.SetCanceled();
            }
        };

        await source.Task;
        return default;
    }
}