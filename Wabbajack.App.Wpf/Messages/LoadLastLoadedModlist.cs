using ReactiveUI;

namespace Wabbajack.Messages;

public class LoadLastLoadedModlist
{
    public static void Send()
    {
        MessageBus.Current.SendMessage(new LoadLastLoadedModlist());
    }
}