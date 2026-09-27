using System;
using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using NLog.Targets;
using Octokit;
using Wabbajack.App.Avalonia.LoginManagers;
using Wabbajack.App.Avalonia.Services;
using Wabbajack.App.Avalonia.Util;
using Wabbajack.App.Avalonia.ViewModels;
using Wabbajack.App.Avalonia.ViewModels.Common;
using Wabbajack.App.Avalonia.ViewModels.Compiler;
using Wabbajack.App.Avalonia.ViewModels.Gallery;
using Wabbajack.App.Avalonia.ViewModels.Installers;
using Wabbajack.App.Avalonia.ViewModels.Settings;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Networking.Bethesda;
using Wabbajack.Networking.Bethesda.Steam;
using Wabbajack.Networking.NexusApi.OAuth;
using Wabbajack.Networking.Steam;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.App.Avalonia;

public static class Program
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>What the app was started with: a wabbajack:// link, a .wabbajack file, or nothing.</summary>
    public static string[] StartupArgs { get; private set; } = [];

    /// <summary>The WPF app's startup, in its order.</summary>
    [STAThread]
    public static int Main(string[] args)
    {
        using var timerResolution = new TimerResolution(1);
        using var singleInstance = new SingleInstance();

        if (!singleInstance.IsFirstInstance)
        {
            // The running instance takes a link from here; this one just leaves.
            if (args.Length > 0) ProtocolPipe.Send(args);
            return 0;
        }

        if (!StartupChecks.EnsureSafeWorkingDirectory())
            return 1;

        if (StartupChecks.IsAdmin())
        {
            NativeMessageBox.ShowError("Don't run Wabbajack as Admin!", "Error");
            return 1;
        }

        var host = Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureLogging(AddLogging)
            .ConfigureServices((_, services) => ConfigureServices(services))
            .Build();
        Services = host.Services;

        // .wabbajack files and wabbajack:// links open this executable, wherever it now lives.
        if (OperatingSystem.IsWindows())
            Services.GetRequiredService<FileAssociationSelfHealService>().RegisterOrUpdate(enableProtocol: true);

        var opensUi = args.Length == 0
                      || StartupChecks.ProtocolPayload(args[0]) != null
                      || (args.Length == 1 && StartupChecks.WabbajackFile(args[0]) != null);
        if (!opensUi)
            return StartupChecks.RunCli(args);

        StartupArgs = args;
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Also what the previewer calls, so it has to stand on its own.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // The default composition surface carries an alpha channel, and Skia will not draw
            // ClearType-style subpixel text onto anything that might be transparent, so text came out
            // greyscale where the WPF app's is subpixel. The window is opaque; a redirection surface says so.
            .With(new Win32PlatformOptions { CompositionMode = [Win32CompositionMode.RedirectionSurface] })
            .LogToTrace()
            .UseReactiveUI();

    /// <summary>What the WPF app's App.ConfigureServices registers.</summary>
    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddOSIntegrated();

        // Registered before AddSteam so it wins over the intervention-based default, which raises a
        // GetAuthCode intervention that the throwing handler below would take a login down over.
        services.AddSingleton<SteamGuardPrompt>();
        services.AddSingleton<ISteamGuardPrompt>(s => s.GetRequiredService<SteamGuardPrompt>());
        services.AddSteam();

        // The second game file source, asked after Steam's: Creations fetched with a ticket from the Steam
        // client the user is already running.
        services.AddBethesdaCreations();
        services.AddSteamAppTicket();

        services.AddSingleton<IUserInterventionHandler, ThrowingUserInterventionHandler>();
        services.AddSingleton<ImageCacheManager>();
        services.AddSingleton<Networking.GitHub.Client>();
        services.AddSingleton(_ => new GitHubClient(new ProductHeaderValue("wabbajack")));

        // One NexusOAuthLogin, because it is what holds "one login at a time".
        services.AddSingleton<NexusOAuthLogin>();
        services.AddTransient<NexusLoginHandler>();
        services.AddAllSingleton<INeedsLogin, NexusLoginManager>();
        services.AddAllSingleton<INeedsLogin, SteamLoginManager>();

        services.AddSingleton<Navigator>();
        services.AddSingleton<MainWindowVM>();
        services.AddTransient<HomeVM>();
        services.AddTransient<SettingsVM>();
        services.AddTransient<CompilerHomeVM>();
        // Transient, as in WPF; MainWindowVM holds the one CompilerMainVM, which holds its own details and file tree.
        services.AddTransient<CompilerDetailsVM>();
        services.AddTransient<CompilerFileManagerVM>();
        services.AddTransient<CompilerMainVM>();
        services.AddTransient<InfoVM>();
        services.AddTransient<FileUploadVM>();
        services.AddTransient<ModListGalleryVM>();
        services.AddSingleton<ModListDetailsVM>();
        services.AddSingleton<GameIconCache>();
        services.AddSingleton<NexusCollectionDownloader>();
        services.AddSingleton<FilePicker>();
        services.AddSingleton<ResourceMonitor>();
        if (OperatingSystem.IsWindows())
            services.AddSingleton<FileAssociationSelfHealService>();
        services.AddSingleton<SystemParametersConstructor>();
        // One instance, held by MainWindowVM from the start: it listens for the modlist to load.
        services.AddTransient<InstallationVM>();
        services.AddTransient<AboutVM>();
        // Transient: each one drives a single login attempt and is thrown away with it.
        services.AddTransient<SteamLoginVM>();
    }

    private static void AddLogging(ILoggingBuilder loggingBuilder)
    {
        var config = new NLog.Config.LoggingConfiguration();

        var logFolder = KnownFolders.LauncherAwarePath.Combine("logs");
        if (!logFolder.DirectoryExists())
            logFolder.CreateDirectory();

        // The same file the WPF app wrote, so support and the installer's "Open log file" find it where they always did.
        var fileTarget = new FileTarget("file")
        {
            FileName = logFolder.Combine("Wabbajack.current.log").ToString(),
            ArchiveFileName = logFolder.Combine("Wabbajack.{##}.log").ToString(),
            ArchiveOldFileOnStartup = true,
            MaxArchiveFiles = 10,
            Layout = "${processtime} [${level:uppercase=true}] (${logger}) ${message:withexception=true}",
            Header = "############ Wabbajack log file - ${longdate} ############"
        };

        config.AddRuleForAllLevels(fileTarget);
        config.AddRuleForAllLevels(new ConsoleTarget("console"));

        // What the installer and compiler log panes show.
        var uiTarget = new LogStream { Name = "ui", Layout = "${message:withexception=false}" };
        loggingBuilder.Services.AddSingleton(uiTarget);
        config.AddRuleForAllLevels(uiTarget);

        loggingBuilder.ClearProviders();
        loggingBuilder.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
        loggingBuilder.SetMinimumLevel(LogLevel.Information);
        loggingBuilder.AddNLog(config);
    }
}
