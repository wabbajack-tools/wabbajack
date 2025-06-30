using ReactiveUI;
using Wabbajack.Compiler;

namespace Wabbajack.Messages;

public class ShowNavigation
{
    public ShowNavigation()
    {
    }

    public static void Send()
    {
        MessageBus.Current.SendMessage(new ShowNavigation());
    }
}