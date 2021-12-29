using ReactiveUI;

namespace Wabbajack.Messages;

public class NavigateToGlobal
{
    public enum ScreenType
    {
        ModeSelectionView,
        ModListGallery,
        Installer,
        Settings,
        Compiler,
        ModListContents
    }

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