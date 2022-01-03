using System;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.Messages;

public class NexusLogin : IUserIntervention
{
    private readonly CancellationTokenSource _source;
    public TaskCompletionSource CompletionSource { get; }
    public CancellationToken Token => _source.Token;
    public void SetException(Exception exception)
    {
        CompletionSource.SetException(exception);
        _source.Cancel();
    }

    public NexusLogin()
    {
        CompletionSource = new TaskCompletionSource();
        _source = new CancellationTokenSource();
        
    }

    public static Task Send()
    {
        var msg = new NexusLogin();
        MessageBus.Current.SendMessage(msg);
        return msg.CompletionSource.Task;
    }

    public void Cancel()
    {
        _source.Cancel();
        CompletionSource.TrySetCanceled();
    }

    public bool Handled => CompletionSource.Task.IsCompleted;
}