using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Extensions;
using Wabbajack.App.Messages;
using Wabbajack.App.Models;
using Wabbajack.App.ViewModels;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.SavedSettings;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.App.Screens;

public class LauncherViewModel : ViewModelBase, IActivatableViewModel, IReceiver<ConfigureLauncher>
{
    private readonly ILogger<LauncherViewModel> _logger;

    public ReactiveCommand<Unit, Unit> PlayButton;

    public LauncherViewModel(ILogger<LauncherViewModel> logger, InstallationStateManager manager)
    {
        Activator = new ViewModelActivator();
        PlayButton = ReactiveCommand.Create(() => { StartGame().FireAndForget(); });
        _logger = logger;

        this.WhenActivated(disposables =>
        {
            this.WhenAnyValue(v => v.InstallFolder)
                .SelectAsync(disposables, async folder => await manager.GetByInstallFolder(folder))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Where(v => v != null)
                .BindTo(this, vm => vm.Setting)
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.Setting)
                .Where(v => v != default)
                .Select(v => new Bitmap(v!.Image.ToString()))
                .BindTo(this, vm => vm.Image)
                .DisposeWith(disposables);

            this.WhenAnyValue(v => v.Setting)
                .Where(v => v is {Metadata: { }})
                .Select(v => $"{v!.Metadata!.Title} v{v!.Metadata.Version}")
                .BindTo(this, vm => vm.Title)
                .DisposeWith(disposables);
        });
    }

    [Reactive] public AbsolutePath InstallFolder { get; set; }

    [Reactive] public IBitmap Image { get; set; }

    [Reactive] public InstallationConfigurationSetting? Setting { get; set; }

    [Reactive] public string Title { get; set; }

    public void Receive(ConfigureLauncher val)
    {
        InstallFolder = val.InstallFolder;
    }

    private async Task StartGame()
    {
        var mo2Path = InstallFolder.Combine("ModOrganizer.exe");
        var gamePath = GameRegistry.Games.Values.Select(g => g.MainExecutable)
            .Where(ge => ge != null)
            .Select(ge => InstallFolder.Combine(ge!))
            .FirstOrDefault(ge => ge.FileExists());
        if (mo2Path.FileExists())
            Process.Start(mo2Path.ToString());
        else if (gamePath.FileExists())
            Process.Start(gamePath.ToString());
        else
            _logger.LogError("No way to launch game, no acceptable executable found");
    }
}