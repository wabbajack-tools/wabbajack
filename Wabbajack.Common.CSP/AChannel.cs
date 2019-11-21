using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.Common.CSP
{
    public abstract class AChannel<TIn, TOut> : IChannel<TIn, TOut>
    {
        public abstract bool IsClosed { get; }
        public abstract void Close();
        public abstract (AsyncResult, bool) Put(TIn val, Handler<Action<bool>> handler);
        public abstract (AsyncResult, TOut) Take(Handler<Action<bool, TOut>> handler);

        private Task<(bool, TOut)> _take_cancelled_task;

        private Task<(bool, TOut)> TakeCancelledTask
        {
            get
            {
                if (_take_cancelled_task == null)
                    _take_cancelled_task = Task.FromCanceled<(bool, TOut)>(CancellationToken.None);
                return _take_cancelled_task;
            }
        }

        private Task<bool> _put_cancelled_task;

        private Task<bool> PutCancelledTask
        {
            get
            {
                if (_put_cancelled_task == null)
                    _put_cancelled_task = Task.FromCanceled<bool>(CancellationToken.None);
                return _put_cancelled_task;
            }
        }

        public ValueTask<(bool, TOut)> Take(bool onCaller) 
        {
            var handler = new TakeTaskHandler<TOut>();
            var (resultType, val) = Take(handler);

            switch (resultType)
            {
                case AsyncResult.Closed:
                    return new ValueTask<(bool, TOut)>((false, default));
                case AsyncResult.Completed:
                    return new ValueTask<(bool, TOut)>((true, val));
                case AsyncResult.Enqueued:
                    return new ValueTask<(bool, TOut)>(handler.TaskCompletionSource.Task);
                case AsyncResult.Canceled:
                    return new ValueTask<(bool, TOut)>(TakeCancelledTask); 
                default:
                    // Should never happen
                    throw new InvalidDataException();
            }
        }

        public ValueTask<bool> Put(TIn val, bool onCaller)
        {
            var handler = new PutTaskHandler<bool>();
            var (resultType, putResult) = Put(val, handler);

            switch (resultType)
            {
                case AsyncResult.Completed:
                    return new ValueTask<bool>(putResult);
                case AsyncResult.Canceled:
                    return new ValueTask<bool>(PutCancelledTask);
                case AsyncResult.Closed:
                    return new ValueTask<bool>(false);
                case AsyncResult.Enqueued:
                    return new ValueTask<bool>(handler.TaskCompletionSource.Task);
                default:
                    // Should never happen
                    throw new InvalidDataException();
            }
        }


    }
}
