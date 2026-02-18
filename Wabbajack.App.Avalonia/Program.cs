using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Wabbajack.App.Avalonia.Interventions;
using Wabbajack.App.Avalonia.Messages;
using Wabbajack.App.Avalonia.ViewModels;
using Wabbajack.App.Avalonia.ViewModels.Gallery;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Interventions;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Services.OSIntegrated.Services;

namespace Wabbajack.App.Avalonia;

internal class Program
{
    public static void Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureLogging(AddLogging)
            .ConfigureServices((_, services) =>
            {
                ConfigureServices(services);
            })
            .Build();

        Services = host.Services;

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    private static void AddLogging(ILoggingBuilder loggingBuilder)
    {
        var logFolder = KnownFolders.WabbajackAppLocal.Combine("logs");
        if (!logFolder.DirectoryExists())
            logFolder.CreateDirectory();

        var config = new NLog.Config.LoggingConfiguration();
        var fileTarget = new NLog.Targets.FileTarget("file")
        {
            FileName = logFolder.Combine("Wabbajack.Avalonia.current.log").ToString(),
            ArchiveFileName = logFolder.Combine("Wabbajack.Avalonia.{##}.log").ToString(),
            ArchiveOldFileOnStartup = true,
            MaxArchiveFiles = 5,
            Layout = "${processtime} [${level:uppercase=true}] (${logger}) ${message:withexception=true}",
        };
        config.AddRuleForAllLevels(fileTarget);

        loggingBuilder.ClearProviders();
        loggingBuilder.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
        loggingBuilder.SetMinimumLevel(LogLevel.Information);
        loggingBuilder.AddNLog(config);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddOSIntegrated();
        services.AddDTOConverters();
        services.AddDTOSerializer();
        services.AddSingleton<IUserInterventionHandler, UserInterventionHandler>();

        services.AddSingleton(s => new Services.OSIntegrated.Configuration
        {
            EncryptedDataLocation = KnownFolders.WabbajackAppLocal.Combine("encrypted"),
            ModListsDownloadLocation = KnownFolders.EntryPoint.Combine("downloaded_mod_lists"),
            SavedSettingsLocation = KnownFolders.WabbajackAppLocal.Combine("saved_settings"),
            LogLocation = KnownFolders.LauncherAwarePath.Combine("logs"),
            ImageCacheLocation = KnownFolders.WabbajackAppLocal.Combine("image_cache"),
        });

        services.AddSingleton<HttpClient>();
        services.AddAllSingleton<IResource, IResource<HttpClient>>(s => new Resource<HttpClient>("Web Requests", 4));

        services.AddSingleton<ImageCacheManager>();
        services.AddSingleton<SettingsManager>();

        services.AddTransient<MainWindowVM>();
        services.AddSingleton<NavigationVM>();
        services.AddTransient<HomeVM>();
        services.AddTransient<ModListGalleryVM>();
        services.AddSingleton<Wabbajack.App.Avalonia.ViewModels.Installer.InstallationVM>();
        services.AddSingleton<Wabbajack.App.Avalonia.ViewModels.Compiler.CompilerVM>();
        services.AddSingleton<Wabbajack.Compiler.CompilerSettingsInferencer>();
    }

    public static IServiceProvider Services { get; private set; } = null!;

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .UseReactiveUI();
}
