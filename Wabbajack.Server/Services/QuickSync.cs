using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;

namespace Wabbajack.Server.Services
{
    public class QuickSync
    {
        private Dictionary<Type, CancellationTokenSource> _syncs = new Dictionary<Type, CancellationTokenSource>();
        private AsyncLock _lock = new AsyncLock();

        public async Task<CancellationToken> GetToken<T>()
        {
            using var _ = await _lock.WaitAsync();
            if (_syncs.TryGetValue(typeof(T), out var result))
            {
                return result.Token;
            }
            var token = new CancellationTokenSource();
            _syncs[typeof(T)] = token;
            return token.Token;
        }

        public async Task ResetToken<T>()
        {
            using var _ = await _lock.WaitAsync();
            if (_syncs.TryGetValue(typeof(T), out var ct))
            {
                ct.Cancel();
            }
            _syncs[typeof(T)] = new CancellationTokenSource();
        }

        public async Task Notify<T>()
        {
            using var _ = await _lock.WaitAsync();
            if (_syncs.TryGetValue(typeof(T), out var ct))
            {
                ct.Cancel();
            }
        }
        
        
        
    }
}
