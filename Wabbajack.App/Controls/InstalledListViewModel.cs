using System.Reactive;
using ReactiveUI;
using Wabbajack.App.Messages;
using Wabbajack.App.Screens;
using Wabbajack.App.ViewModels;
using Wabbajack.DTOs.SavedSettings;
using Wabbajack.Paths;

namespace Wabbajack.App.Controls;

public class InstalledListViewModel : ViewModelBase
{
    private readonly InstallationConfigurationSetting _setting;

    public InstalledListViewModel(InstallationConfigurationSetting setting)
    {
        Activator = new ViewModelActivator();
        _setting = setting;

        Play = ReactiveCommand.Create(() =>
        {
            MessageBus.Instance.Send(new ConfigureLauncher(InstallPath));
            MessageBus.Instance.Send(new NavigateTo(typeof(LauncherViewModel)));
        });
    }

    public AbsolutePath InstallPath => _setting.Install;

    public string Name => _setting.Metadata?.Title ?? "";
    public ReactiveCommand<Unit, Unit> Play { get; }
}