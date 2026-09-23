using System;
using System.Reactive.Subjects;

namespace Wabbajack.App.Avalonia.Services;

public enum ScreenType
{
    Home,
    ModListGallery,
    Compiler,
    Settings,
}

/// <summary>
/// What the WPF app's NavigateToGlobal message did: any view model can ask for a screen, and the main
/// window is the one that listens. A singleton rather than a static bus so it lives and dies with the host.
/// </summary>
public class Navigator
{
    private readonly Subject<ScreenType> _requests = new();

    public IObservable<ScreenType> Requests => _requests;

    public void NavigateTo(ScreenType screen) => _requests.OnNext(screen);
}
