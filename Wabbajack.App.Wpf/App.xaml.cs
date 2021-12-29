using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Lib;
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
        public App()
        {
            var services = new ServiceCollection();
            
            var host = Host.CreateDefaultBuilder(Array.Empty<string>())
                //.ConfigureLogging(c => { c.ClearProviders(); })
                .ConfigureServices((host, services) => { ConfigureServices(services); }).Build();
            
            _serviceProvider = host.Services;
        }
        private IServiceCollection ConfigureServices(IServiceCollection services)
        {
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
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow!.Show();
        }
    }
}
