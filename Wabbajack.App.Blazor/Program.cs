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

        // Only the gallery screen VM is needed; per-tile GalleryModListMetadataVMs are new'd inside it.
        builder.Services.AddTransient<ModListGalleryVM>();

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
