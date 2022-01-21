using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using NLog.Targets;
using Wabbajack.App.Blazor.State;
using Wabbajack.App.Blazor.Utility;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.App.Blazor;

public partial class App
{
    private readonly IServiceProvider _serviceProvider;

    public App()
    {
        _serviceProvider = Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureLogging(SetupLogging)
            .ConfigureServices(services => ConfigureServices(services))
            .Build()
            .Services;
    }

    private static void SetupLogging(ILoggingBuilder loggingBuilder)
    {
        var config = new NLog.Config.LoggingConfiguration();

        var fileTarget = new FileTarget("file")
        {
            FileName = "log.log"
        };
        var consoleTarget = new ConsoleTarget("console");
        var uiTarget = new MemoryTarget("ui");
        var blackholeTarget = new NullTarget("blackhole");
        
        config.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Debug, blackholeTarget, "Microsoft.AspNetCore.Components.*", true);
        config.AddRuleForAllLevels(fileTarget);
        config.AddRuleForAllLevels(consoleTarget);
        config.AddRuleForAllLevels(uiTarget);
        
        loggingBuilder.ClearProviders();
        loggingBuilder.SetMinimumLevel(LogLevel.Trace);
        loggingBuilder.AddNLog(config);
    }
    
    private static IServiceCollection ConfigureServices(IServiceCollection services)
    {
        services.AddOSIntegrated();
        services.AddBlazorWebView();
        services.AddTransient<MainWindow>();
        services.AddSingleton<SystemParametersConstructor>();
        services.AddSingleton<IStateContainer, StateContainer>();
        return services;
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        Current.Shutdown();
    }
}
