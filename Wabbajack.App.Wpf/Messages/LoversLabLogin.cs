using System.Threading.Tasks;
using ReactiveUI;

namespace Wabbajack.Messages;

public class LoversLabLogin : ALoginMessage
{
    
    public static Task Send()
    {
        var msg = new LoversLabLogin();
        MessageBus.Current.SendMessage(msg);
        return msg.CompletionSource.Task;
    }
}