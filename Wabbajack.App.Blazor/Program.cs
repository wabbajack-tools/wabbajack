using System;
using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;
using Wabbajack;
using Wabbajack.Blazor;
using Wabbajack.Blazor.Services;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.Blazor;

// Spike host: a cross-platform Photino.Blazor shell that renders ONLY the modlist gallery by reusing
// the existing Wabbajack.App.Core ViewModels. See docs plan "Photino.Blazor frontend for Wabbajack".
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var builder = PhotinoBlazorAppBuilder.CreateDefault(args);

        builder.Services.AddLogging();

        // Reuse the exact backend graph the Avalonia/WPF heads use.
        builder.Services.AddOSIntegrated();
        builder.Services.AddSingleton<IUserInterventionHandler, ThrowingUserInterventionHandler>();

        // The five platform abstractions Core needs. Only the image service does real work for the
        // gallery spike; the rest are never hit on the gallery path, so they are minimal stubs.
        builder.Services.AddSingleton<IImageService, BlazorImageService>();
        builder.Services.AddSingleton<IDialogService, StubDialogService>();
        builder.Services.AddSingleton<IFileSelector, StubFileSelector>();
        builder.Services.AddSingleton<IClipboardService, StubClipboardService>();
        builder.Services.AddSingleton<ISystemParameters, BlazorSystemParameters>();

        // Shell + screen VMs. NavigationVM is a singleton so the sidebar and the shell share one
        // ActiveScreen. The floating ModListDetailsVM is a singleton so it is already subscribed to
        // LoadModlistForDetails before a gallery tile sends it (otherwise MetadataVM would be null and
        // the panel would NRE — the same ordering bug we hit on the other heads).
        builder.Services.AddSingleton<NavigationVM>();
        builder.Services.AddSingleton<ModListDetailsVM>();
        builder.Services.AddTransient<HomeVM>();
        builder.Services.AddTransient<ModListGalleryVM>();
        builder.Services.AddTransient<InfoVM>();

        // Settings + its dependencies (mirrors the WPF/Avalonia app registrations).
        builder.Services.AddSingleton<Wabbajack.Networking.GitHub.Client>();
        builder.Services.AddSingleton(_ => new Octokit.GitHubClient(new Octokit.ProductHeaderValue("wabbajack")));
        builder.Services.AddSingleton<Wabbajack.LoginManagers.INeedsLogin, Wabbajack.LoginManagers.NexusLoginManager>();
        builder.Services.AddTransient<SettingsVM>();
        builder.Services.AddTransient<AboutVM>();

        builder.RootComponents.Add<App>("#app");

        var app = builder.Build();

        app.MainWindow
            .SetTitle("Wabbajack (Blazor spike)")
            .SetSize(1280, 800);

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Console.Error.WriteLine($"[spike] Unhandled: {e.ExceptionObject}");

        app.Run();
    }
}
