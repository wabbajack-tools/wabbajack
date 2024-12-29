using ReactiveUI;

namespace Wabbajack.Messages;
public class LoadInfoScreen
{
    public string Info { get; set; }
    public ViewModel NavigateBackTarget { get; set; }
    public LoadInfoScreen(string info, ViewModel navigateBackTarget)
    {
        Info = info;
        NavigateBackTarget = navigateBackTarget;
    }
    public static void Send(string info, ViewModel navigateBackTarget)
    {
        NavigateToGlobal.Send(ScreenType.Info);
        MessageBus.Current.SendMessage(new LoadInfoScreen(info, navigateBackTarget));
    }
}
