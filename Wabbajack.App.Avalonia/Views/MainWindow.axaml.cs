using System;
using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Wabbajack.Messages;
using Wabbajack.Services;

namespace Wabbajack.Views;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly CompositeDisposable _disposables = new();

    // Long-lived floating-pane view models. These MUST exist (and be subscribed to their messages)
    // before any screen sends them data: a gallery tile sends LoadModlistForDetails immediately
    // before ShowFloatingWindow, so a freshly-resolved details VM would miss the message and then
    // NRE on MetadataVM when activated. Holding a persistent instance (as the WPF app does via
    // MainWindowVM) means it is already listening when the message arrives.
    private readonly ModListDetailsVM _modListDetailsVM;
    private readonly FileUploadVM _fileUploadVM;

    public MainWindow() : this(Program.Services) { }

    internal MainWindow(IServiceProvider services)
    {
        _services = services;

        InitializeComponent();

        Nav.DataContext = services.GetRequiredService<NavigationVM>();
        ActivePane.Content = services.GetRequiredService<HomeVM>();

        _modListDetailsVM = services.GetRequiredService<ModListDetailsVM>();
        _fileUploadVM = services.GetRequiredService<FileUploadVM>();

        MessageBus.Current.Listen<NavigateToGlobal>()
            .Subscribe(m => Dispatcher.UIThread.Post(() => ActivePane.Content = Resolve(m.Screen)))
            .DisposeWith(_disposables);

        MessageBus.Current.Listen<ShowFloatingWindow>()
            .Subscribe(m => Dispatcher.UIThread.Post(() => ShowFloating(m.Screen)))
            .DisposeWith(_disposables);

        // Windows-only taskbar progress. No-op on other platforms (guarded in the helper).
        MessageBus.Current.Listen<TaskBarUpdate>()
            .Subscribe(m => Dispatcher.UIThread.Post(() => UpdateTaskbar(m)))
            .DisposeWith(_disposables);

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

    protected override void OnClosed(EventArgs e)
    {
        _disposables.Dispose();
        base.OnClosed(e);
    }

    // Test seam: headless tests construct the shell but never Show() it (the embedded Gabarito font
    // can't be shaped by the headless drawing backend), so OnClosed never runs. This lets a test drop
    // the global message-bus subscriptions deterministically. Idempotent with OnClosed's dispose.
    internal void DisposeMessageSubscriptions() => _disposables.Dispose();

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
        var content = FloatingContentFor(screen);
        FloatingPane.Content = content;
        FloatingLayer.IsVisible = content != null;
    }

    // The persistent VM (or null) shown for a given floating screen. Exposed internally so tests can
    // assert the VM is populated before it would be shown — the regression guard for the tile-click crash.
    internal object? FloatingContentFor(FloatingScreenType screen) => screen switch
    {
        FloatingScreenType.ModListDetails => _modListDetailsVM,
        FloatingScreenType.FileUpload => _fileUploadVM,
        _ => null
    };

    private object Resolve(ScreenType screen) => screen switch
    {
        ScreenType.Home => _services.GetRequiredService<HomeVM>(),
        ScreenType.ModListGallery => _services.GetRequiredService<ModListGalleryVM>(),
        ScreenType.Settings => _services.GetRequiredService<SettingsVM>(),
        ScreenType.Installer => _services.GetRequiredService<InstallationVM>(),
        ScreenType.CompilerHome => _services.GetRequiredService<CompilerHomeVM>(),
        ScreenType.CompilerMain => _services.GetRequiredService<CompilerMainVM>(),
        ScreenType.Info => _services.GetRequiredService<InfoVM>(),
        // Other screens resolve to a placeholder until their wave ports them.
        _ => new ScreenPlaceholderView(screen.ToString())
    };
}
