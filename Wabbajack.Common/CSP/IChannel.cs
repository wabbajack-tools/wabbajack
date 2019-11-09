using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common.CSP
{
    public interface IChannel<TIn, TOut>
    {
        bool IsClosed { get; }
        void Close();

        Box<bool> Put(TIn val, Handler<Action<bool>> handler);
        Box<TOut> Take(Handler<Action<Box<TOut>>> handler);
        Task<TOut> Take(bool onCaller = true);
        Task<bool> Put(TIn val, bool onCaller = true);
    }
}
