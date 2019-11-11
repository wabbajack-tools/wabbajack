using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.Common.CSP
{
    public static class CSPExtensions
    {
        public static async Task OntoChannel<TIn, TOut>(this IEnumerable<TIn> coll, IChannel<TIn, TOut> chan)
        {
            foreach (var val in coll)
            {
                if (!await chan.Put(val)) break;
            }
        }

        /// <summary>
        /// Turns a IEnumerable collection into a channel. Note, computation of the enumerable will happen inside
        /// the lock of the channel, so try to keep the work of the enumerable light.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="coll">Collection to spool out of the channel.</param>
        /// <returns></returns>
        public static IChannel<T, T> ToChannel<T>(this IEnumerable<T> coll)
        {
            var chan = Channel.Create(coll.GetEnumerator());
            chan.Close();
            return chan;
        }

        public static IChannel<TOut, TOut> Select<TInSrc, TOutSrc, TOut>(this IChannel<TInSrc, TOutSrc> from, Func<TOutSrc, Task<TOut>> f, bool propagateClose = true)
        {
            var to = Channel.Create<TOut>(4);
            Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        var (is_open_src, val) = await from.Take();
                        if (!is_open_src) break;

                        var is_open_dest = await to.Put(await f(val));
                        if (!is_open_dest) break;
                    }

                }
                finally
                {
                    if (propagateClose)
                    {
                        from.Close();
                        to.Close();
                    }
                }
            });

            return to;
        }

        public static async Task UnorderedParallelDo<T>(this IEnumerable<T> coll, Func<T, Task> f)
        {
            var sink = Channel.CreateSink<bool>();
            await coll.ToChannel()
                .UnorderedPipeline(Environment.ProcessorCount,
                    sink,
                    async itm =>
                    {
                        await f(itm);
                        return true;
                    });
        }

        /// <summary>
        /// Takes all the values from chan, once the channel closes returns a List of the values taken.
        /// </summary>
        /// <typeparam name="TOut"></typeparam>
        /// <typeparam name="TIn"></typeparam>
        /// <param name="chan"></param>
        /// <returns></returns>
        public static async Task<List<TOut>> TakeAll<TOut, TIn>(this IChannel<TIn, TOut> chan)
        {
            List<TOut> acc = new List<TOut>();
            while (true)
            {
                var (open, val) = await chan.Take();
                
                if (!open) break;
                
                acc.Add(val);
            }
            return acc;
        }


        /// <summary>
        /// Pipes values from `from` into `to`
        /// </summary>
        /// <typeparam name="TIn"></typeparam>
        /// <typeparam name="TMid"></typeparam>
        /// <typeparam name="TOut"></typeparam>
        /// <param name="from">source channel</param>
        /// <param name="to">destination channel</param>
        /// <param name="closeOnFinished">Tf true, will close the other channel when one channel closes</param>
        /// <returns></returns>
        public static async Task Pipe<TIn, TMid, TOut>(this IChannel<TIn, TMid> from, IChannel<TMid, TOut> to, bool closeOnFinished = true)
        {
            while (true)
            {
                var (isFromOpen, val) = await from.Take();
                if (isFromOpen)
                {
                    var isToOpen = await to.Put(val);
                    if (isToOpen) continue;
                    if (closeOnFinished)
                        @from.Close();
                    break;
                }
                if (closeOnFinished)
                    to.Close();
                break;
            }
        }

        public static Task<T> ThreadedTask<T>(Func<T> action)
        {
            var src = new TaskCompletionSource<T>();
            var th = new Thread(() =>
            {
                try
                {
                    src.SetResult(action());
                }
                catch (Exception ex)
                {
                    src.SetException(ex);
                }
            }) {Priority = ThreadPriority.BelowNormal};
            th.Start();
            return src.Task;
        }

        public static Task ThreadedTask<T>(Action action)
        {
            var src = new TaskCompletionSource<bool>();
            var th = new Thread(() =>
                {
                    try
                    {
                        action();
                        src.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        src.SetException(ex);
                    }
                })
                { Priority = ThreadPriority.BelowNormal };
            th.Start();
            return src.Task;
        }

    }

}
