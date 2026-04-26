using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.DTOs.Interventions;

public class AUserIntervention<T> : IUserIntervention
{
    private readonly TaskCompletionSource<T> _tcs;
    private readonly CancellationTokenSource _ct;

    protected AUserIntervention()
    {
        _tcs = new TaskCompletionSource<T>();
        _ct = new CancellationTokenSource();
    }
    public void Cancel()
    {
        _ct.Cancel();
        _tcs.SetCanceled(Token);
    }
    
    public bool Handled => _tcs.Task.IsCompleted;
    public CancellationToken Token => _ct.Token;
    public Task<T> Task => _tcs.Task;

    public void Finish(T value)
    {
        _tcs.TrySetResult(value);
        _ct.Cancel();
    }
    
    public void SetException(Exception exception)
    {
        _ct.Cancel();
        _tcs.SetException(exception);
    }
}