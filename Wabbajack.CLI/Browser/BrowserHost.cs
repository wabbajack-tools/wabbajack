using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using CefNet;
using CefNet.Avalonia;
using Microsoft.Extensions.DependencyInjection;

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
    }
}
