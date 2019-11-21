using System;
using System.Threading.Tasks;

namespace Wabbajack.Common.CSP
{
    public interface IWritePort<TIn> : ICloseable
    {
        (AsyncResult, bool) Put(TIn val, Handler<Action<bool>> handler);
        ValueTask<bool> Put(TIn val, bool onCaller = true);
    }
}
