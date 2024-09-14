using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Reactive.Linq;
using System.Windows.Input;
using Wabbajack.Messages;
using Microsoft.Extensions.Logging;
using System.Reactive.Disposables;
using System.Diagnostics;
using System.Reflection;

namespace Wabbajack;

public class NavigationVM : ViewModel
{
    private readonly ILogger<NavigationVM> _logger;
    [Reactive]
    public ScreenType ActiveScreen { get; set; }
    public NavigationVM(ILogger<NavigationVM> logger)
    {
        _logger = logger;
        HomeCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(ScreenType.Home));
        BrowseCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(ScreenType.ModListGallery));
        InstallCommand = ReactiveCommand.Create(() =>
        {
            LoadLastLoadedModlist.Send();
            NavigateToGlobal.Send(ScreenType.Installer);
        });
        CompileModListCommand = ReactiveCommand.Create(() => NavigateToGlobal.Send(ScreenType.CompilerHome));
        SettingsCommand = ReactiveCommand.Create(
            /*
            canExecute: this.WhenAny(x => x.ActivePane)
                .Select(active => !object.ReferenceEquals(active, SettingsPane)),
            */
            execute: () => NavigateToGlobal.Send(ScreenType.Settings));
        MessageBus.Current.Listen<NavigateToGlobal>()
            .Subscribe(x => ActiveScreen = x.Screen)
            .DisposeWith(CompositeDisposable);

        var processLocation = Process.GetCurrentProcess().MainModule?.FileName ?? throw new Exception("Process location is unavailable!");
        var assembly = Assembly.GetExecutingAssembly();
        var assemblyLocation = assembly.Location;
        var fvi = FileVersionInfo.GetVersionInfo(string.IsNullOrWhiteSpace(assemblyLocation) ? processLocation : assemblyLocation);
        Version = $"{fvi.FileVersion}";
    }

    public ICommand HomeCommand { get; }
    public ICommand BrowseCommand { get; }
    public ICommand InstallCommand { get; }
    public ICommand CompileModListCommand { get; }
    public ICommand SettingsCommand { get; }
    public string Version { get; }
}
