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
using Blazored.Modal;
using Blazored.Toast;
using Wabbajack.App.Blazor.Browser.ViewModels;

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
        _serviceProvider.GetRequiredService<SystemParametersConstructor>();
    }

    private static void SetupLogging(ILoggingBuilder loggingBuilder)
    {
        var config = new NLog.Config.LoggingConfiguration();

        var fileTarget = new FileTarget("file")
        {
            FileName = "logs/Wabbajack.current.log",
            ArchiveFileName = "logs/Wabbajack.{##}.log",
            ArchiveOldFileOnStartup = true,
            MaxArchiveFiles = 10,
            Layout = "${processtime} [${level:uppercase=true}] (${logger}) ${message:withexception=true}",
            Header = "############ Wabbajack log file - ${longdate} ############"
        };

        var consoleTarget = new ConsoleTarget("console");

        var uiTarget = new UiLoggerTarget
        {
            Name = "ui",
            Layout = "${message}",
        };

        var blackholeTarget = new NullTarget("blackhole");

        if (!string.Equals("TRUE", Environment.GetEnvironmentVariable("DEBUG_BLAZOR", EnvironmentVariableTarget.Process), StringComparison.OrdinalIgnoreCase))
        {
            config.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Debug, blackholeTarget, "Microsoft.AspNetCore.Components.*", true);
        }

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
        services.AddBlazoredModal();
        services.AddBlazoredToast();
        services.AddTransient<MainWindow>();
        services.AddTransient<NexusLogin>();
        services.AddTransient<VectorPlexus>();
        services.AddTransient<LoversLab>();
        services.AddTransient<BethesdaNetLogin>();
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
