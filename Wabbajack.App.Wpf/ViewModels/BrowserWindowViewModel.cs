using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using HtmlAgilityPack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.DTOs.Interventions;
using Wabbajack.DTOs.Logins;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Messages;
using Wabbajack.Paths;
using Microsoft.Extensions.Logging;

namespace Wabbajack;

public abstract class BrowserWindowViewModel : ViewModel, IClosableVM
{
    private readonly ILogger<BrowserWindowViewModel> _logger;
    private readonly IServiceProvider _serviceProvider;
    private CancellationTokenSource _tokenSource;

    [Reactive] public WebView2 Browser { get; set; }
    [Reactive] public string HeaderText { get; set; }
    [Reactive] public string Instructions { get; set; }
    [Reactive] public string Address { get; set; }
    [Reactive] public ICommand CloseCommand { get; set; }
    [Reactive] public ICommand BackCommand { get; set; }
    [Reactive] public ICommand OpenWebViewHelpCommand { get; set; }
    public event EventHandler Closed;

    public BrowserWindowViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<BrowserWindowViewModel>>();
        BackCommand = ReactiveCommand.Create(() => Browser.GoBack());
        CloseCommand = ReactiveCommand.Create(() => _tokenSource.Cancel());
        OpenWebViewHelpCommand = ReactiveCommand.Create(() => {
            var uri = Consts.WabbajackWebViewWikiUri;
            UIUtils.OpenWebsite(uri);
        });
    }

    public async Task RunBrowserOperation()
    {
        Browser = _serviceProvider.GetRequiredService<WebView2>();

        try
        {
            _tokenSource = new CancellationTokenSource();
            await RunWrapper(_tokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("User manually cancelled browser operation!");
        }
        finally
        {
            Close();
        }
    }

    private void Close()
    {
        _tokenSource.Dispose();
        ShowFloatingWindow.Send(FloatingScreenType.None);
        if(Closed != null)
        {
            foreach(var delegateMethod in Closed.GetInvocationList())
            {
                delegateMethod.DynamicInvoke(this, null);
                Closed -= delegateMethod as EventHandler;
            }
        }
        Activator.Deactivate();
    }

    public async Task RunWrapper(CancellationToken token)
    {
        await Run(token);
    }

    protected abstract Task Run(CancellationToken token);

    protected async Task WaitForReady()
    {
        while (Browser.CoreWebView2 == null)
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

        Browser.NavigationCompleted += Completed;
        Browser.Source = uri;
        await tcs.Task;
        Browser.NavigationCompleted -= Completed;
    }

    public async Task RunJavaScript(string script)
    {
        await Browser.ExecuteScriptAsync(script);
    }

    public async Task<Cookie[]> GetCookies(string domainEnding, CancellationToken token)
    {
        // Strip www. before searching for cookies on a domain to handle websites saving their cookies like .example.org
        if (domainEnding.StartsWith("www."))
        {
            domainEnding = domainEnding[4..];
        }
        var cookies = (await Browser.CoreWebView2.CookieManager.GetCookiesAsync(""))
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
        return await Browser.ExecuteScriptAsync(js);
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
        var referer = Browser.Source;
        while (Browser.CoreWebView2 == null)
            await Task.Delay(10, token);

        EventHandler<CoreWebView2DownloadStartingEventArgs> handler = null!;
        
        handler = (_, args) =>
        {
            try
            {
                source.SetResult(new Uri(args.DownloadOperation.Uri));
                Browser.CoreWebView2.DownloadStarting -= handler;
            }
            catch (Exception)
            {
                source.SetCanceled(token);
                Browser.CoreWebView2.DownloadStarting -= handler;
            }

            args.Cancel = true;
            args.Handled = true;
        };

        Browser.CoreWebView2.DownloadStarting += handler;     
            
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
            Browser.CoreWebView2.Settings.UserAgent);
    }

    public async Task<Hash> WaitForDownload(AbsolutePath path, CancellationToken token)
    {
        var source = new TaskCompletionSource();
        var referer = Browser.Source;
        while (Browser.CoreWebView2 == null)
            await Task.Delay(10, token);

        Browser.CoreWebView2.DownloadStarting += (sender, args) =>
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