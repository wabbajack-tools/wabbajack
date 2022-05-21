using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Interventions;
using Wabbajack.LoginManagers;
using Wabbajack.Models;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.UserIntervention;
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
        private static IServiceCollection ConfigureServices(IServiceCollection services)
        {
            RxApp.MainThreadScheduler = new DispatcherScheduler(Dispatcher.CurrentDispatcher);
            
            services.AddOSIntegrated();

            services.AddSingleton<CefService>();
            services.AddSingleton<IUserInterventionHandler, UserIntreventionHandler>();
            
            services.AddTransient<MainWindow>();
            services.AddTransient<MainWindowVM>();
            services.AddTransient<BrowserWindow>();
            services.AddSingleton<SystemParametersConstructor>();
            services.AddSingleton<LauncherUpdater>();
            services.AddSingleton<ResourceMonitor>();
            services.AddAllSingleton<ILoggerProvider, LoggerProvider>();

            services.AddSingleton<MainSettings>();
            services.AddTransient<CompilerVM>();
            services.AddTransient<InstallerVM>();
            services.AddTransient<ModeSelectionVM>();
            services.AddTransient<ModListGalleryVM>();
            services.AddTransient<CompilerVM>();
            services.AddTransient<InstallerVM>();
            services.AddTransient<SettingsVM>();
            services.AddTransient<WebBrowserVM>();
            
            // Login Handlers
            services.AddTransient<VectorPlexusLoginHandler>();
            services.AddTransient<NexusLoginHandler>();
            services.AddTransient<LoversLabLoginHandler>();
            
            // Login Managers
            services.AddAllSingleton<INeedsLogin, LoversLabLoginManager>();
            services.AddAllSingleton<INeedsLogin, NexusLoginManager>();
            services.AddAllSingleton<INeedsLogin, VectorPlexusLoginManager>();
            services.AddSingleton<ManualDownloadHandler>();
            services.AddSingleton<ManualBlobDownloadHandler>();
            
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
