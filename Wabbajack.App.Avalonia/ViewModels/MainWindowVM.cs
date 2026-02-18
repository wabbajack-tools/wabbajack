using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wabbajack.App.Avalonia.Messages;
using Wabbajack.App.Avalonia.ViewModels.Gallery;
using Wabbajack.App.Avalonia.ViewModels.Installer;

namespace Wabbajack.App.Avalonia.ViewModels;

public class MainWindowVM : ViewModelBase
{
    [Reactive] public ViewModelBase ActiveContent { get; set; }
    [Reactive] public NavigationVM NavigationVM { get; set; }

    public MainWindowVM(HomeVM homeVm, NavigationVM navigationVm, ModListGalleryVM galleryVm, InstallationVM installationVm)
    {
        NavigationVM = navigationVm;
        ActiveContent = homeVm;

        MessageBus.Current.Listen<NavigateToGlobal>()
            .Subscribe(msg => ActiveContent = msg.Screen switch
            {
                ScreenType.Home => (ViewModelBase)homeVm,
                ScreenType.ModListGallery => galleryVm,
                ScreenType.Installer => installationVm,
                _ => ActiveContent
            })
            .DisposeWith(CompositeDisposable);
    }
}
