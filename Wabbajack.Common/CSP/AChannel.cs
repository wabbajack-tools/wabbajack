using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common.CSP
{
    public abstract class AChannel<TIn, TOut> : IChannel<TIn, TOut>
    {
        public abstract bool IsClosed { get; }
        public abstract void Close();
        public abstract Box<bool> Put(TIn val, Handler<Action<bool>> handler);
        public abstract Box<TOut> Take(Handler<Action<Box<TOut>>> handler);

        public Task<TOut> Take(bool onCaller)
        {
            var tcs = new TaskCompletionSource<TOut>();
            var handler = new TakeTaskHandler<TOut>(tcs);
            var result = Take(handler);
            if (result.IsSet)
                tcs.SetResult(result.Value);
            return handler.Task;
        }

        public Task<bool> Put(TIn val, bool onCaller)
        {
            var tcs = new TaskCompletionSource<bool>();
            var handler = new PutTaskHandler<bool>(tcs);
            var result = Put(val, handler);
            if (result.IsSet)
                tcs.SetResult(result.Value);
            return tcs.Task;
        }


    }
}
