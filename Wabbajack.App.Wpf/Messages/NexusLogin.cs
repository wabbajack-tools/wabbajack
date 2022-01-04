using System;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.Messages;

public class NexusLogin : ALoginMessage
{
    public NexusLogin()
    {
    }
    
    public static Task Send()
    {
        var msg = new NexusLogin();
        MessageBus.Current.SendMessage(msg);
        return msg.CompletionSource.Task;
    }
}