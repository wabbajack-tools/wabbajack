using ReactiveUI;
using Wabbajack.Compiler;

namespace Wabbajack.Messages;

public class HideNavigation
{
    public HideNavigation()
    {
    }

    public static void Send()
    {
        MessageBus.Current.SendMessage(new HideNavigation());
    }
}