using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common.CSP
{
    public class TakeTaskHandler<T> : Handler<Action<Box<T>>>
    {
        private readonly bool _blockable;
        private readonly TaskCompletionSource<T> _tcs;

        public TakeTaskHandler(TaskCompletionSource<T> tcs = null, bool blockable = true)
        {
            _blockable = blockable;
            _tcs = tcs ?? new TaskCompletionSource<T>();
        }


        public bool IsActive => true;
        public bool IsBlockable => _blockable;
        public uint LockId => 0;
        public Task<T> Task => _tcs.Task;
        public Action<Box<T>> Commit()
        {
            return Handle;
        }

        private void Handle(Box<T> a)
        {
            if (a.IsSet)
                _tcs.SetResult(a.Value);
            _tcs.SetCanceled();
        }
    }
}
