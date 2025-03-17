using ReactiveUI;

namespace Wabbajack.Messages;

public enum FloatingScreenType
{
    None,
    ModListDetails,
    FileUpload,
    MegaLogin
}

public class ShowFloatingWindow
{
    public FloatingScreenType Screen { get; }
    
    private ShowFloatingWindow(FloatingScreenType screen)
    {
        Screen = screen;
    }

    public static void Send(FloatingScreenType screen)
    {
        MessageBus.Current.SendMessage(new ShowFloatingWindow(screen));
    }

}