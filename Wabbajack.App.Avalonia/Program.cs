using System;
using System.Linq;
using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Octokit;
using Orc.FileAssociation;
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

    [STAThread]
    public static void Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureServices((_, services) => ConfigureServices(services))
            .Build();
        Services = host.Services;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Also used by the Avalonia visual designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .UseReactiveUI();

    // Ported from Wabbajack.App.Wpf/App.xaml.cs ConfigureServices. The WPF WebView2/CefService/
    // BrowserWindow control registrations are handled in Phase 4 (browser host); everything else
    // is the same DI graph the WPF app used.
    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddOSIntegrated();

        services.AddSingleton<IApplicationRegistrationService>(new ApplicationRegistrationService());
        services.AddSingleton<FileAssociationSelfHealService>();

        services.AddSingleton<Interventions.UserInterventionHandler>();
        services.AddSingleton<ImageCacheManager>();
        services.AddSingleton<SystemParametersConstructor>();
        services.AddSingleton<LauncherUpdater>();
        services.AddSingleton<ResourceMonitor>();
        services.AddSingleton<Networking.GitHub.Client>();
        services.AddSingleton(_ => new GitHubClient(new ProductHeaderValue("wabbajack")));

        services.AddSingleton<Views.MainWindow>();
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
