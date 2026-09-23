using System;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.App.Avalonia.Interfaces;
using Wabbajack.App.Avalonia.Messages;
using Wabbajack.App.Avalonia.Services;
using Wabbajack.App.Avalonia.ViewModels.Compiler;
using Wabbajack.App.Avalonia.ViewModels.Gallery;
using Wabbajack.App.Avalonia.ViewModels.Installers;
using Wabbajack.App.Avalonia.ViewModels.Settings;

namespace Wabbajack.App.Avalonia.ViewModels;

public partial class MainWindowVM : ViewModel
{
    private readonly IServiceProvider _services;
    private HomeVM? _home;
    private SettingsVM? _settings;
    private ModListGalleryVM? _gallery;
    private CompilerHomeVM? _compilerHome;
    private readonly PlaceholderVM _compiler = new("Compiler");

    private readonly ModListDetailsVM _modListDetails;
    private readonly InstallationVM _installer;

    public MainWindowVM(IServiceProvider services, Navigator navigator, ModListDetailsVM modListDetails)
    {
        _services = services;
        // Built up front, as in WPF: it listens for the list to show before the pane is first opened.
        _modListDetails = modListDetails;
        // Also up front, as in WPF: it listens for the modlist to load, which arrives before the screen is shown.
        _installer = services.GetRequiredService<InstallationVM>();

        NavigateCommand = ReactiveCommand.Create<ScreenType>(NavigateTo);
        GetHelpCommand = ReactiveCommand.Create(() =>
        {
            if (ActivePane is ICanGetHelpVM pane) pane.GetHelpCommand.Execute(null);
        });
        LoadLocalFileCommand = ReactiveCommand.Create(() =>
        {
            if (ActivePane is ICanLoadLocalFileVM pane) pane.LoadLocalFileCommand.Execute(null);
        });

        navigator.Requests
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(NavigateTo)
            .DisposeWith(CompositeDisposable);

        MessageBus.Current.Listen<ShowFloatingWindow>()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(m => HandleShowFloatingWindow(m.Screen))
            .DisposeWith(CompositeDisposable);

        MessageBus.Current.Listen<ShowNavigation>()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => NavigationVisible = true)
            .DisposeWith(CompositeDisposable);

        MessageBus.Current.Listen<HideNavigation>()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => NavigationVisible = false)
            .DisposeWith(CompositeDisposable);

        MessageBus.Current.Listen<ShowSteamLogin>()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(HandleShowSteamLogin)
            .DisposeWith(CompositeDisposable);

        // A floating pane is live only while it is on screen, as in WPF.
        this.WhenAnyValue(x => x.ActiveFloatingPane)
            .Buffer(2, 1)
            .Subscribe(b =>
            {
                b[0]?.Activator.Deactivate();
                b[1]?.Activator.Activate();
            })
            .DisposeWith(CompositeDisposable);

        this.WhenAnyValue(x => x.ActivePane)
            .Subscribe(pane =>
            {
                ShowGetHelp = pane is ICanGetHelpVM;
                ShowLoadLocalFile = pane is ICanLoadLocalFileVM;
            })
            .DisposeWith(CompositeDisposable);

        var location = Environment.ProcessPath ?? typeof(MainWindowVM).Assembly.Location;
        Version = "v" + FileVersionInfo.GetVersionInfo(location).FileVersion;

        NavigateTo(ScreenType.Home);
    }

    public string WindowTitle => "Wabbajack";
    public string Version { get; }

    public ReactiveCommand<ScreenType, Unit> NavigateCommand { get; }
    public ICommand GetHelpCommand { get; }
    public ICommand LoadLocalFileCommand { get; }

    [Reactive] public partial ViewModel ActivePane { get; private set; }
    [Reactive] public partial ScreenType ActiveScreen { get; private set; }
    [Reactive] public partial ViewModel? ActiveFloatingPane { get; private set; }
    [Reactive] public partial bool ShowGetHelp { get; private set; }
    [Reactive] public partial bool ShowLoadLocalFile { get; private set; }

    /// <summary>False while the installer is running preflight or an install, which takes the nav rail away.</summary>
    [Reactive] public partial bool NavigationVisible { get; private set; } = true;

    /// <summary>
    ///     What Escape and a click outside a floating pane do: ask the pane to close if it knows how, and
    ///     otherwise just take it down.
    /// </summary>
    public void DismissFloatingPane()
    {
        if (ActiveFloatingPane is IClosableVM closable) closable.CloseCommand.Execute(null);
        else ActiveFloatingPane = null;
    }

    /// <summary>
    ///     Shows the Steam login pane until it has an answer, then takes it down if it is still the one on
    ///     screen. The pane belongs to whoever asked for it, so nothing is disposed here.
    /// </summary>
    private async void HandleShowSteamLogin(ShowSteamLogin msg)
    {
        ActiveFloatingPane = msg.ViewModel;
        msg.ViewModel.Start();

        await msg.ViewModel.Result;

        if (ReferenceEquals(ActiveFloatingPane, msg.ViewModel)) ActiveFloatingPane = null;
    }

    private void HandleShowFloatingWindow(FloatingScreenType screen)
    {
        ActiveFloatingPane = screen switch
        {
            FloatingScreenType.None => null,
            FloatingScreenType.ModListDetails => _modListDetails,
            // The file upload pane is not ported yet.
            _ => ActiveFloatingPane
        };
    }

    /// <summary>
    ///     Asks a yes-or-no question in a floating pane and takes the pane down once it is answered. The
    ///     installer asks this through StandardInstaller's OnConfirmAction.
    /// </summary>
    public async Task<bool> ShowConfirmationDialog(string title, string message)
    {
        var dialog = new ConfirmationDialogVM(title, message);
        ActiveFloatingPane = dialog;
        var result = await dialog.Result;
        ActiveFloatingPane = null;
        return result;
    }

    /// <summary>
    ///     What the WPF window did on closing: cancel everything that takes the app-wide token, stop a preflight,
    ///     and give an install or a preflight up to <paramref name="timeout" /> to unwind.
    /// </summary>
    public void CancelRunningTasks(TimeSpan timeout)
    {
        var endTime = DateTime.Now.Add(timeout);
        _services.GetRequiredService<CancellationTokenSource>().Cancel();
        _installer.CancelPreflightForShutdown();

        bool IsInstalling() => _installer.InstallState is InstallState.Installing or InstallState.Preflight;

        // Polled often enough that a preflight, which usually has nothing left to unwind, does not hold the
        // process open for a whole tick after the window has gone.
        while (DateTime.Now < endTime && IsInstalling())
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(100));
        }
    }

    private void NavigateTo(ScreenType screen)
    {
        ActiveScreen = screen;
        ActivePane = screen switch
        {
            ScreenType.Home => _home ??= _services.GetRequiredService<HomeVM>(),
            ScreenType.ModListGallery => _gallery ??= _services.GetRequiredService<ModListGalleryVM>(),
            ScreenType.CompilerHome => _compilerHome ??= _services.GetRequiredService<CompilerHomeVM>(),
            ScreenType.CompilerMain => _compiler,
            ScreenType.Settings => _settings ??= _services.GetRequiredService<SettingsVM>(),
            ScreenType.Installer => _installer,
            // Not ported yet.
            _ => new PlaceholderVM(screen.ToString())
        };
    }
}
