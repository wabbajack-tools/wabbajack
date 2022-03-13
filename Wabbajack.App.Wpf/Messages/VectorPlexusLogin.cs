using System.Threading.Tasks;
using ReactiveUI;

namespace Wabbajack.Messages;

public class VectorPlexusLogin : ALoginMessage
{
     
    public static Task Send()
    {
        var msg = new VectorPlexusLogin();
        MessageBus.Current.SendMessage(msg);
        return msg.CompletionSource.Task;
    }   
}