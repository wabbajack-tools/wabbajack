using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders.Http;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.DTOs.Logins;
using Wabbajack.Launcher.Models;
using Wabbajack.Launcher.ViewModels;
using Wabbajack.Networking.Http;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.NexusApi;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated.TokenProviders;

namespace Wabbajack.Launcher;

// To Build : dotnet publish -r win-x64 -c Release -p:PublishReadyToRun=true --self-contained -o c:\tmp\publish -p:PublishSingleFile=true -p:DebugType=embedded -p:IncludeAllContentForSelfExtract=true
internal class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    public static void Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureLogging(c => { c.ClearProviders(); })
            .ConfigureServices((host, services) =>
            {
                services.AddNexusApi();
                services.AddDTOConverters();
                services.AddDTOSerializer();
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<HttpClient>();
                services.AddSingleton<ITokenProvider<NexusApiState>, NexusApiTokenProvider>();
                services.AddSingleton<HttpDownloader>();
                services.AddAllSingleton<IResource, IResource<HttpClient>>(s => new Resource<HttpClient>("Web Requests", 4));
                services.AddAllSingleton<IHttpDownloader, SingleThreadedDownloader>();
                
                var version =
                    $"{ThisAssembly.Git.SemVer.Major}.{ThisAssembly.Git.SemVer.Major}.{ThisAssembly.Git.SemVer.Patch}{ThisAssembly.Git.SemVer.DashLabel}";
                services.AddSingleton(s => new ApplicationInfo
                {
                    ApplicationSlug = "Wabbajack",
                    ApplicationName = Environment.ProcessPath?.ToAbsolutePath().FileName.ToString() ?? "Wabbajack",
                    ApplicationSha = ThisAssembly.Git.Sha,
                    Platform = RuntimeInformation.ProcessArchitecture.ToString(),
                    OperatingSystemDescription = RuntimeInformation.OSDescription,
                    RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier,
                    OSVersion = Environment.OSVersion.VersionString,
                    Version = version
                });
            }).Build();
        Services = host.Services;
        
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }
    public static IServiceProvider Services { get; set; }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .UseReactiveUI();
    }
}