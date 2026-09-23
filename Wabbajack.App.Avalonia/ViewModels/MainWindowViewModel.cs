using System;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.App.Avalonia.Services;

namespace Wabbajack.App.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;
    private HomeViewModel? _home;
    private readonly PlaceholderViewModel _gallery = new("Browse lists");
    private readonly PlaceholderViewModel _compiler = new("Create a list");
    private readonly PlaceholderViewModel _settings = new("Settings");

    public MainWindowViewModel(IServiceProvider services, Navigator navigator)
    {
        _services = services;

        NavigateCommand = ReactiveCommand.Create<ScreenType>(NavigateTo);
        GetHelpCommand = ReactiveCommand.Create(() => Links.Open(Links.Wiki));

        navigator.Requests
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(NavigateTo);

        var location = Environment.ProcessPath ?? typeof(MainWindowViewModel).Assembly.Location;
        Version = "v" + FileVersionInfo.GetVersionInfo(location).FileVersion;

        NavigateTo(ScreenType.Home);
    }

    public string WindowTitle => "Wabbajack";
    public string Version { get; }

    public ReactiveCommand<ScreenType, Unit> NavigateCommand { get; }
    public ReactiveCommand<Unit, Unit> GetHelpCommand { get; }

    [Reactive] public partial ViewModelBase ActivePane { get; private set; }
    [Reactive] public partial ScreenType ActiveScreen { get; private set; }

    private void NavigateTo(ScreenType screen)
    {
        ActiveScreen = screen;
        ActivePane = screen switch
        {
            ScreenType.Home => _home ??= _services.GetRequiredService<HomeViewModel>(),
            ScreenType.ModListGallery => _gallery,
            ScreenType.Compiler => _compiler,
            ScreenType.Settings => _settings,
            _ => throw new ArgumentOutOfRangeException(nameof(screen), screen, null)
        };
    }
}
