using System;
using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wabbajack.DTOs.Interventions;
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
                services.AddSingleton<IImageService, AvaloniaImageService>();
                services.AddTransient<HomeVM>(); // Core VMs the Avalonia app currently uses
                services.AddTransient<NavigationVM>();
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
