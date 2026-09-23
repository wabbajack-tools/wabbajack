using System;
using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using NLog.Targets;
using Wabbajack.App.Avalonia.Services;
using Wabbajack.App.Avalonia.ViewModels;
using Wabbajack.App.Avalonia.Views;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.DTOs.Interventions;

namespace Wabbajack.App.Avalonia;

public static class Program
{
    public static IServiceProvider Services { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureLogging(AddLogging)
            .ConfigureServices((_, services) => ConfigureServices(services))
            .Build();
        Services = host.Services;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Also what the previewer calls, so it has to stand on its own.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .UseReactiveUI();

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddOSIntegrated();
        services.AddSingleton<IUserInterventionHandler, ThrowingUserInterventionHandler>();

        services.AddSingleton<Navigator>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<HomeViewModel>();
    }

    private static void AddLogging(ILoggingBuilder loggingBuilder)
    {
        var config = new NLog.Config.LoggingConfiguration();

        var logFolder = KnownFolders.LauncherAwarePath.Combine("logs");
        if (!logFolder.DirectoryExists())
            logFolder.CreateDirectory();

        // Named apart from the WPF app's log: while both exist they can share a logs folder, and each
        // archives the other's file on startup otherwise.
        var fileTarget = new FileTarget("file")
        {
            FileName = logFolder.Combine("Wabbajack.Avalonia.current.log").ToString(),
            ArchiveFileName = logFolder.Combine("Wabbajack.Avalonia.{##}.log").ToString(),
            ArchiveOldFileOnStartup = true,
            MaxArchiveFiles = 10,
            Layout = "${processtime} [${level:uppercase=true}] (${logger}) ${message:withexception=true}",
            Header = "############ Wabbajack log file - ${longdate} ############"
        };

        config.AddRuleForAllLevels(fileTarget);
        config.AddRuleForAllLevels(new ConsoleTarget("console"));

        loggingBuilder.ClearProviders();
        loggingBuilder.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
        loggingBuilder.SetMinimumLevel(LogLevel.Information);
        loggingBuilder.AddNLog(config);
    }
}
