using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common.CSP
{ 
    public interface IReadPort<TOut> : ICloseable
    {
        ValueTask<(bool, TOut)> Take(bool onCaller = true);
        (AsyncResult, TOut) Take(Handler<Action<bool, TOut>> handler);

    }
}
