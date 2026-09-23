using System;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.App.Avalonia.Interfaces;
using Wabbajack.App.Avalonia.Messages;
using Wabbajack.App.Avalonia.Services;
using Wabbajack.App.Avalonia.ViewModels.Compiler;
using Wabbajack.App.Avalonia.ViewModels.Settings;

namespace Wabbajack.App.Avalonia.ViewModels;

public partial class MainWindowVM : ViewModel
{
    private readonly IServiceProvider _services;
    private HomeVM? _home;
    private SettingsVM? _settings;
    private readonly PlaceholderVM _gallery = new("Browse lists");
    private CompilerHomeVM? _compilerHome;
    private readonly PlaceholderVM _compiler = new("Compiler");

    public MainWindowVM(IServiceProvider services, Navigator navigator)
    {
        _services = services;

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

    private void NavigateTo(ScreenType screen)
    {
        ActiveScreen = screen;
        ActivePane = screen switch
        {
            ScreenType.Home => _home ??= _services.GetRequiredService<HomeVM>(),
            ScreenType.ModListGallery => _gallery,
            ScreenType.CompilerHome => _compilerHome ??= _services.GetRequiredService<CompilerHomeVM>(),
            ScreenType.CompilerMain => _compiler,
            ScreenType.Settings => _settings ??= _services.GetRequiredService<SettingsVM>(),
            // Not ported yet.
            _ => new PlaceholderVM(screen.ToString())
        };
    }
}
