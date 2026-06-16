using ReactiveUI;

namespace Wabbajack.Messages;

public class NavigateBack
{
    public static void Send()
    {
        MessageBus.Current.SendMessage(new NavigateBack());
    }
}