using System;
using System.Data.SQLite;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.App.Avalonia.Messages;
using Wabbajack.App.Avalonia.Services;
using Wabbajack.App.Avalonia.ViewModels;
using Wabbajack.App.Avalonia.Views;
using Wabbajack.Common;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

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
            var window = new MainWindow { DataContext = CreateMainWindowVM() };
            desktop.MainWindow = window;

            HandleStartupArgs(Program.StartupArgs);
            var pipe = new CancellationTokenSource();
            ProtocolPipe.Listen(args => Dispatcher.UIThread.Post(() =>
            {
                BringToFront(window);
                // As in WPF, a later launch is only acted on for a wabbajack:// link.
                if (args.Length > 0 && StartupChecks.ProtocolPayload(args[0]) != null)
                    HandleStartupArgs(args);
            }), Program.Services.GetRequiredService<ILogger<App>>(), pipe.Token);
            desktop.Exit += (_, _) => pipe.Cancel();

            // WPF's default MaxDropDownHeight: a third of the primary screen's height in device-independent
            // pixels (SystemParameters.PrimaryScreenHeight / 3). Combo boxes read it from here.
            var screen = window.Screens.Primary;
            Resources["WpfMaxDropDownHeight"] = screen is null ? 400.0 : screen.Bounds.Height / screen.Scaling / 3;

            // As the WPF window did on opening; it does nothing outside a launcher-laid-out install.
            RunLauncherUpdater();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    ///     A wabbajack:// link opens the gallery and loads that list; a .wabbajack file opens the installer with it.
    ///     With neither, the window stays on Home.
    /// </summary>
    private static void HandleStartupArgs(string[] args)
    {
        if (args.Length == 0) return;
        var navigator = Program.Services.GetRequiredService<Navigator>();
        var logger = Program.Services.GetRequiredService<ILogger<App>>();

        if (StartupChecks.ProtocolPayload(args[0]) is { } payload)
        {
            logger.LogInformation("Handling protocol URL: {url}", args[0]);
            navigator.NavigateTo(ScreenType.ModListGallery);
            LoadModlistFromProtocol.Send(payload);
            return;
        }

        if (args.Length == 1 && StartupChecks.WabbajackFile(args[0]) is { } file)
        {
            LoadModlistForInstalling.Send(file, null);
            navigator.NavigateTo(ScreenType.Installer);
        }
    }

    private static void BringToFront(Window window)
    {
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }

    /// <summary>
    ///     WPF's OpenUI: settings under %localappdata%\Wabbajack that cannot be opened show up here, as SQLite
    ///     refusing to open its file, and the user is offered a repair and a restart. Anything else is shown and
    ///     rethrown.
    /// </summary>
    private static MainWindowVM CreateMainWindowVM()
    {
        try
        {
            return Program.Services.GetRequiredService<MainWindowVM>();
        }
        catch (Exception ex)
        {
            if (OperatingSystem.IsWindows() && ex is SQLiteException { ResultCode: SQLiteErrorCode.CantOpen } &&
                NativeMessageBox.AskYesNo(
                    "Wabbajack cannot read or write to settings files inside %localappdata%/Wabbajack! Let Wabbajack adjust permissions?",
                    "Failed to start Wabbajack"))
            {
                StartupChecks.RepairSettingsFolderAndRestart(KnownFolders.WabbajackAppLocal);
            }

            NativeMessageBox.ShowError($"Wabbajack failed to start! Full exception: {ex}", "Failed to start Wabbajack");
            throw;
        }
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
