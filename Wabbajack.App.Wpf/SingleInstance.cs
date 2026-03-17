using System;
using System.Threading;

namespace Wabbajack
{
    public class SingleInstance : IDisposable
    {
        private readonly Mutex _mutex;
        private bool _owned;

        public SingleInstance(string uniqueName)
        {
            _mutex = new Mutex(true, uniqueName, out _owned);
        }

        public bool IsFirstInstance => _owned;

        public void Dispose()
        {
            if (_owned)
            {
                _mutex?.ReleaseMutex();
                _owned = false;
            }
            _mutex?.Dispose();
        }
    }
}