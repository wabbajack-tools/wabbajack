using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.App.Avalonia.Services;
using Wabbajack.App.Avalonia.ViewModels;
using Wabbajack.App.Avalonia.Views;

namespace Wabbajack.App.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // WPF's combo box list highlights one row: the selected one when it opens, then whichever the pointer
        // is on, because keyboard focus follows the pointer there. The highlight style keys off focus.
        InputElement.PointerEnteredEvent.AddClassHandler<ComboBoxItem>((item, _) => item.Focus());
        Control.LoadedEvent.AddClassHandler<ComboBoxItem>((item, _) =>
        {
            if (item.IsSelected) item.Focus();
        });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow
            {
                DataContext = Program.Services.GetRequiredService<MainWindowVM>()
            };
            desktop.MainWindow = window;

            // WPF's default MaxDropDownHeight: a third of the primary screen's height in device-independent
            // pixels (SystemParameters.PrimaryScreenHeight / 3). Combo boxes read it from here.
            var screen = window.Screens.Primary;
            Resources["WpfMaxDropDownHeight"] = screen is null ? 400.0 : screen.Bounds.Height / screen.Scaling / 3;

            // As the WPF window did on opening; it does nothing outside a launcher-laid-out install.
            RunLauncherUpdater();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void RunLauncherUpdater()
    {
        var services = Program.Services;
        var updater = ActivatorUtilities.CreateInstance<LauncherUpdater>(services);
        var logger = services.GetRequiredService<ILogger<LauncherUpdater>>();
        Task.Run(async () =>
        {
            try
            {
                await updater.Run();
            }
            catch (Exception ex)
            {
                // WPF dropped this exception unobserved; a failed update check should at least be in the log.
                logger.LogWarning(ex, "Could not check for a launcher update");
            }
        });
    }
}
