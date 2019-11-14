using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.Common.CSP
{
    public class TakeTaskHandler<T> : Handler<Action<bool, T>>
    {
        private readonly bool _blockable;
        private TaskCompletionSource<(bool, T)> _tcs;

        public TakeTaskHandler(TaskCompletionSource<T> tcs = null, bool blockable = true)
        {
            _blockable = blockable;
        }

        public TaskCompletionSource<(bool, T)> TaskCompletionSource
        {
            get
            {
                if (_tcs == null)
                {
                    var new_tcs = new TaskCompletionSource<(bool, T)>();
                    Interlocked.CompareExchange(ref _tcs, new_tcs, null);
                }

                return _tcs;
            }
        }


        public bool IsActive => true;
        public bool IsBlockable => _blockable;
        public uint LockId => 0;
        public Task<(bool, T)> Task => TaskCompletionSource.Task;
        public Action<bool, T> Commit()
        {
            return Handle;
        }

        private void Handle(bool is_open, T a)
        {
            TaskCompletionSource.SetResult((is_open, a));
        }
    }
}
