using System;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Avalonia.Messages;

namespace Wabbajack.App.Avalonia.ViewModels;

public class NavigationVM : ViewModelBase
{
    private readonly ILogger<NavigationVM> _logger;

    [Reactive] public ScreenType ActiveScreen { get; set; }

    public ICommand HomeCommand { get; }
    public ICommand BrowseCommand { get; }
    public string Version { get; }

    public NavigationVM(ILogger<NavigationVM> logger)
    {
        _logger = logger;

        HomeCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(ScreenType.Home));
        BrowseCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(ScreenType.ModListGallery));

        MessageBus.Current.Listen<NavigateToGlobal>()
            .Subscribe(x => ActiveScreen = x.Screen)
            .DisposeWith(CompositeDisposable);

        var processLocation = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        var assemblyLocation = typeof(NavigationVM).Assembly.Location;
        var fvi = FileVersionInfo.GetVersionInfo(string.IsNullOrWhiteSpace(assemblyLocation) ? processLocation : assemblyLocation);
        Version = fvi.FileVersion ?? "0.0.0.0";
    }
}
