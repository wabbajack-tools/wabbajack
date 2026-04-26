using System;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.Common;

public static class SemaphoreSlimExtensions
{
    public static async Task<IDisposable> Lock(this SemaphoreSlim slim)
    {
        await slim.WaitAsync();
        return Disposable.Create(() => slim.Release());
    }
}