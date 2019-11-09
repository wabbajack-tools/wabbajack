using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Wabbajack.Common.CSP
{
    public struct Box<T>
    {
        public T Value;
        public bool IsSet;

        public Box(T value)
        {
            Value = value;
            IsSet = true;
        }


        public static Box<T> Empty = new Box<T>();
    }

    class test : IValueTaskSource
    {
        public ValueTaskSourceStatus GetStatus(short token)
        {
            throw new NotImplementedException();
        }

        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            throw new NotImplementedException();
        }

        public void GetResult(short token)
        {
            throw new NotImplementedException();
        }
    }
}
