using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;

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

        /*
        private static void PipelineInner<TInSrc, TOutSrc, TInDest, TOutDest>(int n,
            IChannel<TInSrc, TOutSrc> from,
            Func<TOutSrc, Task<TInDest>> fn,
            IChannel<TInDest, TOutDest> to,
            bool closeOnFinished)
        {
            var jobs = Channel.Create<TOutSrc>(n);
            var results = Channel.Create<TInDest>(n);

            {
                bool Process(TOutSrc val, )
                {
                    if ()

                }
            }
        }*/


        /// <summary>
        /// Creates a pipeline that takes items from `from` transforms them with the pipeline given by `transform` and puts
        /// the resulting values onto `to`. The pipeline may create 0 or more items for every input item and they will be
        /// spooled onto `to` in a undefined order. `n` determines how many parallel tasks will be running at once. Each of
        /// these tasks maintains its own transformation pipeline, so `transform` will be called once for every `n`. Completing
        /// a `transform` pipeline has no effect.
        /// </summary>
        /// <typeparam name="TInSrc"></typeparam>
        /// <typeparam name="TOutSrc"></typeparam>
        /// <typeparam name="TInDest"></typeparam>
        /// <typeparam name="TOutDest"></typeparam>
        /// <param name="from"></param>
        /// <param name="parallelism"></param>
        /// <param name="to"></param>
        /// <param name="transform"></param>
        /// <param name="propagateClose"></param>
        /// <returns></returns>
        public static async Task UnorderedPipeline<TInSrc, TOutSrc, TInDest, TOutDest>(
            this IChannel<TInSrc, TOutSrc> from,
            int parallelism,
            IChannel<TInDest, TOutDest> to,
            Func<IObservable<TOutSrc>, IObservable<TInDest>> transform,
            bool propagateClose = true)
        {
            async Task Pump()
            {
                var pipeline = new Subject<TOutSrc>();
                var buffer = new List<TInDest>();
                var dest = transform(pipeline);
                dest.Subscribe(itm => buffer.Add(itm));
                while (true)
                {
                    var (is_open, tval) = await from.Take();
                    if (is_open)
                    {
                        pipeline.OnNext(tval);
                        foreach (var pval in buffer)
                        {
                            var is_put_open = await to.Put(pval);
                            if (is_put_open) continue;
                            if (propagateClose) @from.Close();
                            return;
                        }
                        buffer.Clear();
                    }
                    else
                    {
                        pipeline.OnCompleted();
                        if (buffer.Count > 0)
                        {
                            foreach (var pval in buffer)
                                if (!await to.Put(pval))
                                    break;
                        }
                        break;
                    }
                }
            }

            await Task.WhenAll(Enumerable.Range(0, parallelism)
                .Select(idx => Task.Run(Pump)));

            if (propagateClose)
            {
                from.Close();
                to.Close();
            }

        }
    }

}
