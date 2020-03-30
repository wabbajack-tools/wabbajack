using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.Common
{
    public class AsyncBlockingCollection<T> : IDisposable
    {
        private readonly ConcurrentQueue<T> _collection;
        private bool isDisposed = false;

        public AsyncBlockingCollection()
        {
            _collection = new ConcurrentQueue<T>();
        }
        
        public void Add(T val)
        {
            _collection.Enqueue(val);
        }

        public async ValueTask<(bool found, T val)> TryTake(TimeSpan timeout, CancellationToken token)
        {
            var startTime = DateTime.Now;
            while (true)
            {
                if (_collection.TryDequeue(out T result))
                {
                    return (true, result);
                }

                if (DateTime.Now - startTime > timeout || token.IsCancellationRequested || isDisposed)
                    return (false, default);
                await Task.Delay(100);
            }
        }

        public void Dispose()
        {
            isDisposed = true;
        }
    }
}
