using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common.CSP
{
    class PutTaskHandler<T> : Handler<Action<bool>>
    {
        private readonly bool _blockable;
        private readonly TaskCompletionSource<bool> _tcs;

        public PutTaskHandler(TaskCompletionSource<bool> tcs, bool blockable = true)
        {
            _blockable = blockable;
            _tcs = tcs ?? new TaskCompletionSource<bool>();
        }

        public bool IsActive => true;
        public bool IsBlockable => _blockable;
        public uint LockId => 0;
        public Action<bool> Commit()
        {
            return Handle;
        }

        private void Handle(bool val)
        {
            _tcs.SetResult(val);
        }
    }
}
