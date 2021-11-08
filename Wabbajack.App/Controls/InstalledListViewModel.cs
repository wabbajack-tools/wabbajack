using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Messages;
using Wabbajack.App.Models;
using Wabbajack.App.Screens;
using Wabbajack.App.ViewModels;
using Wabbajack.Common;
using Wabbajack.DTOs.SavedSettings;
using Wabbajack.Paths;

namespace Wabbajack.App.Controls;

public class InstalledListViewModel : ViewModelBase
{
    private readonly InstallationConfigurationSetting _setting;

    public InstalledListViewModel(InstallationConfigurationSetting setting, ImageCache imageCache)
    {
        Activator = new ViewModelActivator();
        _setting = setting;

        Play = ReactiveCommand.Create(() =>
        {
            MessageBus.Current.SendMessage(new ConfigureLauncher(InstallPath));
            MessageBus.Current.SendMessage(new NavigateTo(typeof(LauncherViewModel)));
        });

        LoadImage(imageCache).FireAndForget();
    }

    public async Task LoadImage(ImageCache cache)
    {
        var img = await cache.From(_setting.Install.Combine("modlist-image.png"), 270, 150);
        Dispatcher.UIThread.Post(() =>
        {
            Image = img;
        });
    }

    public AbsolutePath InstallPath => _setting.Install;

    public string Name => _setting.Metadata?.Title ?? "";

    public string Version => _setting.Metadata?.Version?.ToString() ?? "";

    public string Author => _setting.Metadata?.Author ?? "";
    public ReactiveCommand<Unit, Unit> Play { get; }
    
    [Reactive]
    public IBitmap Image { get; set; }
}