using System;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs.Interventions;

namespace Wabbajack.Messages;

public class ALoginMessage : IUserIntervention
{
    private readonly CancellationTokenSource _source;
    public TaskCompletionSource CompletionSource { get; }
    public CancellationToken Token => _source.Token;
    public void SetException(Exception exception)
    {
        CompletionSource.SetException(exception);
        _source.Cancel();
    }

    public ALoginMessage()
    {
        CompletionSource = new TaskCompletionSource();
        _source = new CancellationTokenSource();
        
    }
    
    public void Cancel()
    {
        _source.Cancel();
        CompletionSource.TrySetCanceled();
    }

    public bool Handled => CompletionSource.Task.IsCompleted;
}