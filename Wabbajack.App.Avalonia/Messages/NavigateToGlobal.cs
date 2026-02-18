using ReactiveUI;

namespace Wabbajack.App.Avalonia.Messages;

public enum ScreenType
{
    Home,
    ModListGallery,
    Installer,
    Compiler,
}

public class NavigateToGlobal
{
    public ScreenType Screen { get; }

    private NavigateToGlobal(ScreenType screen)
    {
        Screen = screen;
    }

    public static void Send(ScreenType screen)
    {
        MessageBus.Current.SendMessage(new NavigateToGlobal(screen));
    }
}
