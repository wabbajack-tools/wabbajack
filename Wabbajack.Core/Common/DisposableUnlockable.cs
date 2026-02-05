using System;

namespace Wabbajack.Common;

public struct DisposableUnlockable : IDisposable
{
    private readonly IUnlockable _unlock;

    public DisposableUnlockable(IUnlockable unlock)
    {
        _unlock = unlock;
    }

    public void Dispose()
    {
        _unlock.Unlock();
    }
}