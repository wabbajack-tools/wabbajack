using System;
using System.Linq;
using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Octokit;
using Orc.FileAssociation;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Wabbajack.Paths.IO;
using Wabbajack.CLI;
using Wabbajack.DTOs;
using Wabbajack.Interventions;
using Wabbajack.LoginManagers;
using Wabbajack.Models;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.UserIntervention;
using Wabbajack.Util;

namespace Wabbajack;

internal static class Program
{
    public static IServiceProvider Services { get; private set; } = null!;

    private static SingleInstance? _singleInstance;

    [STAThread]
    public static void Main(string[] args)
    {
        // Single-instance guard, matching the WPF app. A second launch just exits; the running
        // instance keeps ownership of the protocol/file-association handling.
        _singleInstance = new SingleInstance("Wabbajack-{F8C1E8F0-3E3A-4B3D-9F4A-1E5C6D7E8F9A}");
        if (!_singleInstance.IsFirstInstance)
        {
            Environment.Exit(0);
            return;
        }

        var host = Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureLogging(AddLogging)
            .ConfigureServices((_, services) => ConfigureServices(services))
            .Build();
        Services = host.Services;

        if (OperatingSystem.IsWindows())
        {
            // .wabbajack file association + wabbajack:// protocol registration.
            Services.GetRequiredService<FileAssociationSelfHealService>().RegisterOrUpdate(enableProtocol: true);
        }

        StartupArgs = args;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Command-line arguments the app was launched with (protocol URL, .wabbajack file, ...).</summary>
    public static string[] StartupArgs { get; private set; } = Array.Empty<string>();

    // Also used by the Avalonia visual designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .UseReactiveUI();

    // Ported from Wabbajack.App.Wpf/App.xaml.cs AddLogging: file + console + the in-app LogStream
    // target (which is also registered so view models can bind the log view).
    private static void AddLogging(ILoggingBuilder loggingBuilder)
    {
        var config = new NLog.Config.LoggingConfiguration();

        var logFolder = KnownFolders.LauncherAwarePath.Combine("logs");
        if (!logFolder.DirectoryExists())
            logFolder.CreateDirectory();

        var fileTarget = new NLog.Targets.FileTarget("file")
        {
            FileName = logFolder.Combine("Wabbajack.current.log").ToString(),
            ArchiveFileName = logFolder.Combine("Wabbajack.{##}.log").ToString(),
            ArchiveOldFileOnStartup = true,
            MaxArchiveFiles = 10,
            Layout = "${processtime} [${level:uppercase=true}] (${logger}) ${message:withexception=true}",
            Header = "############ Wabbajack log file - ${longdate} ############"
        };

        var consoleTarget = new NLog.Targets.ConsoleTarget("console");

        var uiTarget = new LogStream
        {
            Name = "ui",
            Layout = "${message:withexception=false}",
        };

        loggingBuilder.Services.AddSingleton(uiTarget);

        config.AddRuleForAllLevels(fileTarget);
        config.AddRuleForAllLevels(consoleTarget);
        config.AddRuleForAllLevels(uiTarget);

        loggingBuilder.ClearProviders();
        loggingBuilder.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
        loggingBuilder.SetMinimumLevel(LogLevel.Information);
        loggingBuilder.AddNLog(config);
    }

    // Ported from Wabbajack.App.Wpf/App.xaml.cs ConfigureServices. The WPF WebView2/CefService/
    // BrowserWindow control registrations are handled in Phase 4 (browser host); everything else
    // is the same DI graph the WPF app used.
    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddOSIntegrated();

        services.AddSingleton<IApplicationRegistrationService>(new ApplicationRegistrationService());
        services.AddSingleton<FileAssociationSelfHealService>();

        services.AddSingleton<DTOs.Interventions.IUserInterventionHandler, Interventions.UserInterventionHandler>();
        services.AddSingleton<Abstractions.IFilePicker, AvaloniaFilePicker>();
        services.AddSingleton<ImageCacheManager>();
        services.AddSingleton<SystemParametersConstructor>();
        services.AddSingleton<LauncherUpdater>();
        services.AddSingleton<ResourceMonitor>();
        services.AddSingleton<Networking.GitHub.Client>();
        services.AddSingleton(_ => new GitHubClient(new ProductHeaderValue("wabbajack")));

        // Browser host: a single shared WebView2 (WebView2.Avalonia) re-parented into BrowserWindow
        // per operation, matching the WPF app. Honour a local ./WebView2 runtime folder if present.
        services.AddSingleton(_ =>
        {
            var browser = new Avalonia.Controls.WebView2();
            var localRuntime = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "WebView2");
            if (System.IO.Directory.Exists(localRuntime))
            {
                browser.CreationProperties = new Avalonia.Controls.CoreWebView2CreationProperties
                {
                    BrowserExecutableFolder = localRuntime
                };
            }
            return browser;
        });
        services.AddSingleton<Views.BrowserWindow>();

        services.AddSingleton<MainWindow>();
        services.AddTransient<MainWindowVM>();
        services.AddTransient<NavigationVM>();
        services.AddTransient<HomeVM>();
        services.AddTransient<ModListGalleryVM>();
        services.AddTransient<CompilerHomeVM>();
        services.AddTransient<CompilerDetailsVM>();
        services.AddTransient<CompilerFileManagerVM>();
        services.AddTransient<CompilerMainVM>();
        services.AddTransient<InstallationVM>();
        services.AddTransient<SettingsVM>();
        services.AddTransient<WebBrowserVM>();
        services.AddTransient<InfoVM>();
        services.AddTransient<ModListDetailsVM>();
        services.AddTransient<FileUploadVM>();
        services.AddTransient<MegaLoginVM>();
        services.AddTransient<AboutVM>();

        services.AddTransient<VectorPlexusLoginHandler>();
        services.AddTransient<NexusLoginHandler>();
        services.AddTransient<LoversLabLoginHandler>();

        services.AddAllSingleton<INeedsLogin, NexusLoginManager>();
        services.AddAllSingleton<INeedsLogin, MegaLoginManager>();
        services.AddSingleton<ManualDownloadHandler>();
        services.AddSingleton<ManualBrowserDownloadHandler>();
        services.AddSingleton<NexusCollectionDownloader>();

        services.AddSingleton<System.CommandLine.Builder.CommandLineBuilder>();
        services.AddCLIVerbs();
    }
}
