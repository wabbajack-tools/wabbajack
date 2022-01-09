using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.App.Blazor
{
    public partial class App
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHost            _host;

        public App()
        {
            _host = Host.CreateDefaultBuilder(Array.Empty<string>())
                .ConfigureLogging(c => { c.ClearProviders(); })
                .ConfigureServices((host, services) => { ConfigureServices(services); })
                .Build();

            _serviceProvider = _host.Services;
        }

        private static IServiceCollection ConfigureServices(IServiceCollection services)
        {
            services.AddBlazorWebView();
            services.AddOSIntegrated();
            services.AddTransient<MainWindow>();
            return services;
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            MainWindow mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow!.Show();
        }

        private void OnExit(object sender, ExitEventArgs e)
        {
            Current.Shutdown();
            // using (_host)
            // {
            //     _host.StopAsync();
            // }
            //
            // base.OnExit(e);
        }
    }
}