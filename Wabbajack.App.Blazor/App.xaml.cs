using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wabbajack.App.Blazor.Models;
using Wabbajack.App.Blazor.State;
using Wabbajack.App.Blazor.Utility;
using Wabbajack.DTOs;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.App.Blazor;

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
        services.AddOSIntegrated();
        services.AddBlazorWebView();
        services.AddAllSingleton<ILoggerProvider, LoggerProvider>();
        services.AddTransient<MainWindow>();
        services.AddSingleton<SystemParametersConstructor>();
        services.AddSingleton<GlobalState>();
        return services;
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
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
