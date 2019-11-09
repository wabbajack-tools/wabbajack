using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common.CSP
{
    public class FnHandler<T> : Handler<T>
    {
        private readonly bool _blockable;
        private T _f;

        public FnHandler(T f, bool blockable=false)
        {
            _f = f;
            _blockable = blockable;
        }

        public bool IsActive => true;
        public bool IsBlockable => _blockable;
        public uint LockId => 0;
        public T Commit()
        {
            return _f;
        }
    }
}
