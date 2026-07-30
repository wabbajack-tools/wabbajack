using System;
using System.Reactive.Concurrency;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Wabbajack.Messages;

namespace Wabbajack;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // MainWindow takes its view model through the DI constructor.
            desktop.MainWindow = Program.Services.GetRequiredService<MainWindow>();
            HandleStartupArgs(Program.StartupArgs);
        }

        base.OnFrameworkInitializationCompleted();
    }

    // Ported from the WPF App.OnStartup argument handling: a wabbajack:// protocol URL navigates to
    // the gallery and loads the requested list; a .wabbajack file path just opens the UI.
    private static void HandleStartupArgs(string[] args)
    {
        if (args.Length == 0) return;

        var first = args[0];
        if (!first.StartsWith("wabbajack://", StringComparison.OrdinalIgnoreCase)) return;

        var payload = Uri.UnescapeDataString(first["wabbajack://".Length..]).Trim();
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            NavigateToGlobal.Send(ScreenType.ModListGallery);
            LoadModlistFromProtocol.Send(payload);
        });
    }
}
