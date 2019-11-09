using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using ICSharpCode.SharpZipLib.Zip;
using YamlDotNet.Serialization.NodeTypeResolvers;

namespace Wabbajack.Common.CSP
{
    /// <summary>
    /// An almost 1:1 port of Clojure's core.async channels
    /// </summary>
    
    public class ManyToManyChannel<TIn, TOut> : AChannel<TIn, TOut>
    {
        public const int MAX_QUEUE_SIZE = 1024;

        private RingBuffer<Handler<Action<bool, TOut>>> _takes = new RingBuffer<Handler<Action<bool, TOut>>>(8);
        private RingBuffer<(Handler<Action<bool>>, TIn)> _puts = new RingBuffer<(Handler<Action<bool>>, TIn)>(8);
        private IBuffer<TOut> _buf;
        private Func<IBuffer<TOut>, TIn, bool> _add;
        private Action<IBuffer<TOut>> _finalize;
        private Func<TIn, TOut> _converter;
        bool _isClosed = false;

        public ManyToManyChannel(Func<TIn, TOut> converter)
        {
            _buf = null;
            _add = null;
            _finalize = null;
            _converter = converter;
        }

        public ManyToManyChannel(Func<TIn, TOut> converter, Func<IBuffer<TOut>, TIn, bool> add, Action<IBuffer<TOut>> finalize, IBuffer<TOut> buffer)
        {
            _buf = buffer;
            _add = add;
            _finalize = finalize;
            _converter = converter;
        }

        private static bool IsActiveTake(Handler<Action<bool, TOut>> handler)
        {
            return handler.IsActive;
        }

        private static bool IsActivePut((Handler<Action<bool>>, TIn) input)
        {
            return input.Item1.IsActive;
        }

        /// <summary>
        /// Tries to put a put into the channel
        /// </summary>
        /// <param name="val"></param>
        /// <param name="handler"></param>
        /// <returns>(result_type, w)</returns>
        public override (AsyncResult, bool) Put(TIn val, Handler<Action<bool>> handler)
        {
            Monitor.Enter(this);
            if (_isClosed)
            {
                Monitor.Exit(this);
                return (AsyncResult.Completed, false);
            }

            if (_buf != null && !_buf.IsFull && !_takes.IsEmpty)
            {
                var put_cb = LockIfActiveCommit(handler);
                if (put_cb != null)
                {
                    var is_done = _add(_buf, val);
                    if (!_buf.IsEmpty)
                    {
                        var take_cbs = GetTakersForBuffer();
                        if (is_done)
                            Abort();
                        Monitor.Exit(this);
                        foreach (var action in take_cbs)
                        {
                            Task.Run(action);
                        }
                    }
                    else
                    {
                        if (is_done)
                            Abort();
                        Monitor.Exit(this);
                        return (AsyncResult.Closed, false);
                    }

                    return (AsyncResult.Completed, true);
                }
                Monitor.Exit(this);
                return (AsyncResult.Canceled, false);
            }

            var (put_cb2, take_cb) = GetCallbacks(handler, _takes);

            if (put_cb2 != null && take_cb != null)
            {
                Monitor.Exit(this);
                Task.Run(() => take_cb(true, _converter(val)));
                return (AsyncResult.Completed, true);
            }

            if (_buf != null && !_buf.IsFull)
            {
                if (LockIfActiveCommit(handler) != null)
                {
                    if (_add(_buf, val))
                    {
                        Abort();
                    }
                    Monitor.Exit(this);
                    return (AsyncResult.Completed, true);
                }
                Monitor.Exit(this);
                return (AsyncResult.Canceled, true);
            }

            if (handler.IsActive && handler.IsBlockable)
            {
                if (_puts.Length >= MAX_QUEUE_SIZE)
                {
                    Monitor.Exit(this);
                    throw new TooManyHanldersException();
                }
                _puts.Unshift((handler, val));
            }
            Monitor.Exit(this);
            return (AsyncResult.Enqueued, true);
        }

        public override (AsyncResult, TOut) Take(Handler<Action<bool, TOut>> handler)
        {
            Monitor.Enter(this);
            Cleanup();

            if (_buf != null && !_buf.IsEmpty)
            {
                var take_cb = LockIfActiveCommit(handler);
                if (take_cb != null)
                {
                    var val = _buf.Remove();
                    var (is_done, cbs) = GetPuttersForBuffer();

                    if (is_done)
                        Abort();

                    Monitor.Exit(this);

                    foreach (var cb in cbs)
                        Task.Run(() => cb(true));

                    return (AsyncResult.Completed, val);

                }
                Monitor.Exit(this);
                return (AsyncResult.Canceled, default);
            }

            var (take_cb2, put_cb, val2, found) = FindMatchingPut(handler);

            if (take_cb2 != null && put_cb != null)
            {
                Monitor.Exit(this);
                Task.Run(() => put_cb(true));
                return (AsyncResult.Completed, _converter(val2));
            }

            if (_isClosed)
            {
                if (_buf != null && found)
                    _add(_buf, val2);

                var has_val = _buf != null && !_buf.IsEmpty;
                var take_cb3 = LockIfActiveCommit(handler);

                if (take_cb3 != null)
                {
                    var val = has_val ? _buf.Remove() : default;
                    Monitor.Exit(this);
                    return has_val ? (AsyncResult.Completed, val) : (AsyncResult.Closed, default);
                }
                Monitor.Exit(this);
                return (AsyncResult.Closed, default);
            }

            if (handler.IsBlockable)
            {
                if (_takes.Length >= MAX_QUEUE_SIZE)
                    throw new TooManyHanldersException();
                _takes.Unshift(handler);

            }
            Monitor.Exit(this);
            return (AsyncResult.Enqueued, default);
        }

        public override bool IsClosed => _isClosed;

        public override void Close()
        {
            Monitor.Enter(this);
            Cleanup();
            if (_isClosed)
            {
                Monitor.Exit(this);
                return;
            }

            _isClosed = true;
            if (_buf != null && _puts.IsEmpty)
                _finalize(_buf);
            var cbs = GetTakersForBuffer();

            while (!_takes.IsEmpty)
            {
                var take_cb = LockIfActiveCommit(_takes.Pop());
                if (take_cb != null)
                    cbs.Add(() => take_cb(false, default));
            }
            
            Monitor.Exit(this);
            
            foreach (var cb in cbs)
                Task.Run(cb);
        }

        private (Action<bool, TOut>, Action<bool>, TIn, bool) FindMatchingPut(Handler<Action<bool, TOut>> handler)
        {
            while (!_puts.IsEmpty)
            {
                var (found, val) = _puts.Peek();
                var (handler_cb, put_cb, handler_active, put_active) = LockIfActiveCommit(handler, found);

                if (handler_active && put_active)
                {
                    _puts.Pop();
                    return (handler_cb, put_cb, val, true);
                }

                if (!put_active)
                {
                    _puts.Pop();
                    continue; 
                }

                return (null, null, default, false);

            }

            return (null, null, default, false);
        }

        private (bool, List<Action<bool>>) GetPuttersForBuffer()
        {
            List<Action<bool>> acc = new List<Action<bool>>();

            while (!_puts.IsEmpty)
            {
                var (putter, val) = _puts.Pop();
                var cb = LockIfActiveCommit(putter);
                if (cb != null)
                {
                    acc.Add(cb);
                }

                var is_done = _add(_buf, val);
                if (is_done || _buf.IsFull || _puts.IsEmpty) 
                    return (is_done, acc);
            }

            return (false, acc);
        }

        private void Cleanup()
        {
            _takes.Cleanup(IsActiveTake);
            _puts.Cleanup(IsActivePut);
        }

        private (T1, T2) GetCallbacks<T1, T2>(Handler<T1> handler, RingBuffer<Handler<T2>> queue)
        {
            while (!queue.IsEmpty)
            {
                var found = queue.Peek();
                var (handler_cb, found_cb, handler_valid, found_valid) = LockIfActiveCommit(handler, found);
                if (handler_valid && found_valid)
                {
                    queue.Pop();
                    return (handler_cb, found_cb);
                }

                if (handler_valid)
                {
                    queue.Pop();
                }
                else
                {
                    return (default, default);
                }
            }

            return (default, default);
        }

        private void Abort()
        {
            while (!_puts.IsEmpty)
            {
                var (handler, val) = _puts.Pop();
                var put_cb = LockIfActiveCommit(handler);
                if (put_cb != null)
                {
                    Task.Run(() => put_cb(true));
                }
            }
            _puts.Cleanup(x => false);
            Close();
        }

        private List<Action> GetTakersForBuffer()
        {
            List<Action> ret = new List<Action>();
            while (!_buf.IsEmpty && !_takes.IsEmpty)
            {
                var taker = _takes.Pop();
                var take_cp = LockIfActiveCommit(taker);
                if (take_cp != null)
                {
                    var val = _buf.Remove();
                    ret.Add(() => take_cp(true, val));
                }
            }

            return ret;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T IfActiveCommit<T>(Handler<T> handler)
        {
            return handler.IsActive ? handler.Commit() : default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T LockIfActiveCommit<T>(Handler<T> handler)
        {
            lock (handler)
            {
                return handler.IsActive ? handler.Commit() : default;
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static (T1, T2, bool, bool) LockIfActiveCommit<T1, T2>(Handler<T1> handler1, Handler<T2> handler2)
        {
            if (handler1.LockId < handler2.LockId)
            {
                Monitor.Enter(handler1);
                Monitor.Enter(handler2);
            }
            else
            {
                Monitor.Enter(handler2);
                Monitor.Enter(handler1);
            }

            if (handler1.IsActive && handler2.IsActive)
            {
                var ret1 = (handler1.Commit(), handler2.Commit(), true, true);
                Monitor.Exit(handler1);
                Monitor.Exit(handler2);
                return ret1;
            }

            var ret2 = (default(T1), default(T2), handler1.IsActive, handler2.IsActive);
            Monitor.Exit(handler1);
            Monitor.Exit(handler2);
            return ret2;
        }

        public class TooManyHanldersException : Exception
        {
            public override string ToString()
            {
                return $"No more than {MAX_QUEUE_SIZE} pending operations allowed on a single channel.";
            }

        }
    }


}
