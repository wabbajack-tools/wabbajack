using System.Threading.Tasks;
using ReactiveUI;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.Messages;

public class NexusLogin
{
    private TaskCompletionSource CompletionSource { get; }

    public NexusLogin()
    {
        CompletionSource = new TaskCompletionSource();
    }

    public static Task Send()
    {
        var msg = new NexusLogin();
        MessageBus.Current.SendMessage(msg);
        return msg.CompletionSource.Task;
    }
}