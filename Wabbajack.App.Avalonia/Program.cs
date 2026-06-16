using System;
using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Interventions;
using Wabbajack.LoginManagers;
using Wabbajack.Models;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack;

internal class Program
{
    public static IServiceProvider Services { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureServices((_, services) =>
            {
                services.AddOSIntegrated();
                services.AddSingleton<IUserInterventionHandler, ThrowingUserInterventionHandler>();
                services.AddSingleton<IFileSelector, AvaloniaFileSelector>();
                services.AddSingleton<IDialogService, AvaloniaDialogService>();
                services.AddSingleton<IClipboardService, AvaloniaClipboardService>();
                services.AddSingleton<IImageService, AvaloniaImageService>();
                services.AddSingleton<ISystemParameters, AvaloniaSystemParameters>();
                services.AddTransient<HomeVM>(); // Core VMs the Avalonia app currently uses
                services.AddTransient<NavigationVM>();
                services.AddTransient<ModListGalleryVM>();
                services.AddTransient<SettingsVM>();
                services.AddTransient<AboutVM>();
                services.AddTransient<InstallationVM>();
                services.AddTransient<CompilerHomeVM>();
                services.AddTransient<CompilerMainVM>();
                services.AddTransient<CompilerDetailsVM>();
                services.AddTransient<CompilerFileManagerVM>();
                services.AddTransient<ModListDetailsVM>();
                services.AddTransient<FileUploadVM>();
                services.AddTransient<InfoVM>();
                // InstallationVM needs these singletons that AddOSIntegrated doesn't provide.
                services.AddSingleton<ResourceMonitor>();
                services.AddSingleton<LogStream>();
                // LogStream not wired as an NLog target yet (deferred): the log view just renders empty.
                // AboutVM pulls the GitHub contributor list; register the client like the WPF app does.
                services.AddSingleton<Networking.GitHub.Client>();
                services.AddSingleton(_ => new Octokit.GitHubClient(new Octokit.ProductHeaderValue("wabbajack")));
                // Nexus OAuth login: registering an INeedsLogin populates the Settings login list.
                services.AddAllSingleton<INeedsLogin, NexusLoginManager>();
            }).Build();
        Services = host.Services;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .UseReactiveUI();
}
