using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common.CSP
{
    public class TakeTaskHandler<T> : Handler<Action<T>>
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
                    _tcs = new TaskCompletionSource<(bool, T)>();
                return _tcs;
            }
        }


        public bool IsActive => true;
        public bool IsBlockable => _blockable;
        public uint LockId => 0;
        public Task<(bool, T)> Task => TaskCompletionSource.Task;
        public Action<T> Commit()
        {
            return Handle;
        }

        private void Handle(T a)
        {
            TaskCompletionSource.SetResult((true, a));
        }
    }
}
