using System;
using System.Reactive.Subjects;
using Wabbajack.App.Avalonia.ViewModels;

namespace Wabbajack.App.Avalonia.Services;

/// <summary>The WPF app's ScreenType, under the same names.</summary>
public enum ScreenType
{
    Home,
    ModListGallery,
    Installer,
    CompilerHome,
    CompilerMain,
    ModListDetails,
    Settings,
    Info
}

/// <summary>
/// What the WPF app's NavigateToGlobal message did: any view model can ask for a screen, and the main
/// window is the one that listens. A singleton rather than a static bus so it lives and dies with the host.
/// </summary>
public class Navigator
{
    private readonly Subject<ScreenType> _requests = new();
    private readonly Subject<ViewModel> _paneRequests = new();

    public IObservable<ScreenType> Requests => _requests;

    /// <summary>The WPF app's NavigateTo message: show this pane, leaving the nav rail's highlight where it is.</summary>
    public IObservable<ViewModel> PaneRequests => _paneRequests;

    public void NavigateTo(ScreenType screen) => _requests.OnNext(screen);

    public void NavigateTo(ViewModel pane) => _paneRequests.OnNext(pane);

    /// <summary>
    /// The nav rail item a screen belongs to, as NavigationView's button map had it: the installer lights
    /// Browse lists, and both compiler screens light Create a list.
    /// </summary>
    public static ScreenType NavItemFor(ScreenType screen) => screen switch
    {
        ScreenType.ModListGallery or ScreenType.Installer => ScreenType.ModListGallery,
        ScreenType.CompilerHome or ScreenType.CompilerMain => ScreenType.CompilerHome,
        _ => screen
    };
}
