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

public abstract class BrowserWindowViewModel : ViewModel
{
    [Reactive] public string HeaderText { get; set; }

    [Reactive] public string Instructions { get; set; }

    [Reactive] public string Address { get; set; }

    public BrowserWindow? Browser { get; set; }

    private Microsoft.Web.WebView2.Wpf.WebView2 _browser => Browser!.Browser;

    public async Task RunWrapper(CancellationToken token)
    {
        await Run(token);
        //MessageBus.Current.SendMessage(new CloseBrowserTab(this));
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
        Address = uri.ToString();

        void Completed(object? o, CoreWebView2NavigationCompletedEventArgs a)
        {
            if (a.IsSuccess)
            {
                tcs.TrySetResult();
            }
            else
            {
                if (a.WebErrorStatus is CoreWebView2WebErrorStatus.ConnectionAborted or CoreWebView2WebErrorStatus.Unknown )
                {
                    tcs.TrySetResult();
                }
                else
                {
                    tcs.TrySetException(new Exception($"Navigation error to {uri} - {a.WebErrorStatus}"));
                }
            }
        }

        _browser.NavigationCompleted += Completed;
        _browser.Source = uri;
        await tcs.Task;
        _browser.NavigationCompleted -= Completed;
    }

    public async Task RunJavaScript(string script)
    {
        await _browser.ExecuteScriptAsync(script);
    }

    public async Task<Cookie[]> GetCookies(string domainEnding, CancellationToken token)
    {
        // Strip www. before searching for cookies on a domain to handle websites saving their cookies like .example.org
        if (domainEnding.StartsWith("www."))
        {
            domainEnding = domainEnding[4..];
        }
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

    public async Task<ManualDownload.BrowserDownloadState> WaitForDownloadUri(CancellationToken token, Func<Task>? whileWaiting)
    {
        var source = new TaskCompletionSource<Uri>();
        var referer = _browser.Source;
        while (_browser.CoreWebView2 == null)
            await Task.Delay(10, token);

        _browser.CoreWebView2.DownloadStarting += (sender, args) =>
        {
            try
            {
                source.SetResult(new Uri(args.DownloadOperation.Uri));
            }
            catch (Exception)
            {
                source.SetCanceled();
            }

            args.Cancel = true;
            args.Handled = true;
        };
        Uri uri;

        while (true)
        {
            try
            {
                uri = await source.Task.WaitAsync(TimeSpan.FromMilliseconds(250), token);
                break;
            }
            catch (TimeoutException)
            {
                if (whileWaiting != null)
                    await whileWaiting();
            }
        }

        var cookies = await GetCookies(uri.Host, token);
        return new ManualDownload.BrowserDownloadState(
            uri,
            cookies,
            new[]
            {
                ("Referer", referer?.ToString() ?? uri.ToString())
            },
            _browser.CoreWebView2.Settings.UserAgent);
    }

    public async Task<Hash> WaitForDownload(AbsolutePath path, CancellationToken token)
    {
        var source = new TaskCompletionSource();
        var referer = _browser.Source;
        while (_browser.CoreWebView2 == null)
            await Task.Delay(10, token);

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
            catch (Exception)
            {
                source.SetCanceled();
            }
        };

        await source.Task;
        return default;
    }
}