using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using CefNet;
using CefNet.Avalonia;
using HtmlAgilityPack;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.DTOs.Logins;

namespace Wabbajack.CLI.Browser
{
    public class BrowserHost
    {
        private TaskCompletionSource _startupTask = new();
        private readonly IServiceProvider _serviceProvider;

        public BrowserHost(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            var th = new Thread(() =>
            {
                BuildAvaloniaApp()
                    .StartWithCefNetApplicationLifetime(Array.Empty<string>(), ShutdownMode.OnExplicitShutdown);
            });
            th.SetApartmentState(ApartmentState.STA);
            th.Start();
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure(() => new BrowserApp
                {
                    Provider = _serviceProvider,
                    OnComplete = _startupTask
                })
                .UsePlatformDetect()
                .LogToTrace()
                .UseReactiveUI();

        public async Task<BrowserState> CreateBrowser()
        {
            await _startupTask.Task;
            var tcs = new TaskCompletionSource<BrowserState>();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var window = _serviceProvider.GetRequiredService<MainWindow>();
                window.ViewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
                window.Show();
                tcs.SetResult(new BrowserState(window, window.ViewModel, window.Browser));
            });
            return await tcs.Task;
        }
    }

    public class BrowserState
    {
        private MainWindow _mainWindow;
        private MainWindowViewModel _mainWindowViewModel;
        private readonly CustomWebView _browser;

        public BrowserState(MainWindow mainWindow, MainWindowViewModel vm, CustomWebView browser)
        {
            _mainWindow = mainWindow;
            _mainWindowViewModel = vm;
            _browser = browser;
        }

        public string Instructions
        {
            set
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _mainWindowViewModel.Instructions = value;
                });
            }
        }

        public async Task WaitForReady()
        {
            while (!_browser.IsInitialized)
            {
                await Task.Delay(250);
            }

            while (_browser.BrowserObject == null)
            {
                await Task.Delay(250);
            }
        }

        public async Task NavigateTo(Uri location)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _browser.Navigate(location.ToString());
            });
            await WaitForIdle();
        }

        public async Task WaitForIdle()
        {
            while (_browser.IsBusy)
            {
                await Task.Delay(250);
            }
        }

        public async Task<Cookie[]> Cookies(string domainEnding, CancellationToken token)
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

        public async Task EvaluateJavaScript(string js)
        {
            _browser.GetMainFrame().ExecuteJavaScript(js, "", 0);
        }

        public async Task<HtmlDocument> GetDom(CancellationToken token)
        {
            var source = await _browser.GetMainFrame().GetSourceAsync(token);
            var doc = new HtmlDocument();
            doc.LoadHtml(source);
            return doc;
        }
    }
}