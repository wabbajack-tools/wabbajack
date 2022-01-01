using ReactiveUI;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.Messages;

public class NexusLogin
{
    public NexusLogin()
    {
        
    }

    public static void Send()
    {
        MessageBus.Current.SendMessage(new NexusLogin());
    }
}