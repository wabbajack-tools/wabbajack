using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Threading;
using CefSharp.DevTools.Debugger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Splat;
using Wabbajack.Common;
using Wabbajack;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Util;

namespace Wabbajack
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHost _host;

        public App()
        {
            _host = Host.CreateDefaultBuilder(Array.Empty<string>())
                .ConfigureLogging(c =>
                {
                    c.ClearProviders();
                })
                .ConfigureServices((host, services) =>
                {
                    ConfigureServices(services);
                })
                .Build();
            
            _serviceProvider = _host.Services;
        }
        private IServiceCollection ConfigureServices(IServiceCollection services)
        {
            RxApp.MainThreadScheduler = new DispatcherScheduler(Dispatcher.CurrentDispatcher);
            
            services.AddOSIntegrated();
            services.AddTransient<MainWindow>();
            services.AddTransient<MainWindowVM>();
            services.AddSingleton<SystemParametersConstructor>();
            services.AddSingleton<LauncherUpdater>();

            services.AddSingleton<MainSettings>();
            services.AddTransient<CompilerVM>();
            services.AddTransient<InstallerVM>();
            services.AddTransient<ModeSelectionVM>();
            services.AddTransient<ModListGalleryVM>();
            
            return services;
        }
        private void OnStartup(object sender, StartupEventArgs e)
        {
            RxApp.MainThreadScheduler.Schedule(0, (_, _) =>
            {
                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                mainWindow!.Show();
                return Disposable.Empty;
            });
        }

        private void OnExit(object sender, ExitEventArgs e)
        {
            using (_host)
            {
                _host.StopAsync();
            }
            base.OnExit(e);
        }
    }
}
