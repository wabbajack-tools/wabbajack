using System;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Wabbajack.Messages;
using Wabbajack.Services;

namespace Wabbajack.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Nav.DataContext = Program.Services.GetRequiredService<NavigationVM>();
        ActivePane.Content = Program.Services.GetRequiredService<HomeVM>();

        MessageBus.Current.Listen<NavigateToGlobal>()
            .Subscribe(m => Dispatcher.UIThread.Post(() => ActivePane.Content = Resolve(m.Screen)));

        MessageBus.Current.Listen<ShowFloatingWindow>()
            .Subscribe(m => Dispatcher.UIThread.Post(() => ShowFloating(m.Screen)));

        // Windows-only taskbar progress. No-op on other platforms (guarded in the helper).
        MessageBus.Current.Listen<TaskBarUpdate>()
            .Subscribe(m => Dispatcher.UIThread.Post(() => UpdateTaskbar(m)));

        // Dismiss the floating pane on Escape or a click on the dimmed backdrop.
        KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape && FloatingLayer.IsVisible)
                ShowFloating(FloatingScreenType.None);
        };
        FloatingBackdrop.PointerPressed += (_, _) => ShowFloating(FloatingScreenType.None);

        // Debug/screenshot aid: launch directly into a screen via the WJ_SCREEN env var (e.g. "Installer").
        var initialScreen = Environment.GetEnvironmentVariable("WJ_SCREEN");
        if (!string.IsNullOrEmpty(initialScreen) && Enum.TryParse<ScreenType>(initialScreen, out var screen))
            Dispatcher.UIThread.Post(() => NavigateToGlobal.Send(screen));
    }

    private void UpdateTaskbar(TaskBarUpdate update)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            var hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (hwnd == IntPtr.Zero) return;
            WindowsTaskbarProgress.Update(hwnd, update.State, update.ProgressValue);
        }
        catch
        {
            // COM calls can fail; never let taskbar updates crash the app.
        }
    }

    private void ShowFloating(FloatingScreenType screen)
    {
        object? content = screen switch
        {
            FloatingScreenType.ModListDetails => Program.Services.GetRequiredService<ModListDetailsVM>(),
            FloatingScreenType.FileUpload => Program.Services.GetRequiredService<FileUploadVM>(),
            _ => null
        };
        FloatingPane.Content = content;
        FloatingLayer.IsVisible = content != null;
    }

    private object Resolve(ScreenType screen) => screen switch
    {
        ScreenType.Home => Program.Services.GetRequiredService<HomeVM>(),
        ScreenType.ModListGallery => Program.Services.GetRequiredService<ModListGalleryVM>(),
        ScreenType.Settings => Program.Services.GetRequiredService<SettingsVM>(),
        ScreenType.Installer => Program.Services.GetRequiredService<InstallationVM>(),
        ScreenType.CompilerHome => Program.Services.GetRequiredService<CompilerHomeVM>(),
        ScreenType.CompilerMain => Program.Services.GetRequiredService<CompilerMainVM>(),
        ScreenType.Info => Program.Services.GetRequiredService<InfoVM>(),
        // Other screens resolve to a placeholder until their wave ports them.
        _ => new ScreenPlaceholderView(screen.ToString())
    };
}
