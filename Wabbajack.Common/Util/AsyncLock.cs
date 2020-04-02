using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.Common
{
    public class AsyncLock
    {
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public async Task<IDisposable> Wait()
        {
            await _lock.WaitAsync();
            return Disposable.Create(() => _lock.Release());
        }
    }
}
