using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.Common.CSP
{
    public static class Pipelines
    {


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
        public static async Task UnorderedPipeline<TIn, TOut>(
            this IReadPort<TIn> from,
            int parallelism,
            IWritePort<TOut> to,
            Func<IObservable<TIn>, IObservable<TOut>> transform,
            bool propagateClose = true)
        {
            async Task Pump()
            {
                var pipeline = new Subject<TIn>();
                var buffer = new List<TOut>();
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

        public static async Task UnorderedPipeline<TIn, TOut>(
            this IReadPort<TIn> from,
            IWritePort<TOut> to,
            Func<TIn, Task<TOut>> f,
            bool propagateClose = true)
        {
            await UnorderedPipeline(from, Environment.ProcessorCount, to, f, propagateClose);
        }

        public static async Task UnorderedPipeline<TIn, TOut>(
            this IReadPort<TIn> from,
            int parallelism,
            IWritePort<TOut> to,
            Func<TIn, Task<TOut>> f,
            bool propagateClose = true)
        {
            async Task Pump()
            {
                while (true)
                {
                    var (is_open, job) = await from.Take();
                    if (!is_open) break;

                    var putIsOpen = await to.Put(await f(job));
                    if (!putIsOpen) return;
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

        public static IReadPort<TOut> UnorderedPipelineRx<TIn, TOut>(
            this IReadPort<TIn> from,
            Func<IObservable<TIn>, IObservable<TOut>> f,
            bool propagateClose = true)
        {
            var parallelism = Environment.ProcessorCount;
            var to = Channel.Create<TOut>(parallelism * 2);
            var pipeline = from.UnorderedPipeline(parallelism, to, f);
            return to;

        }

        public static IReadPort<TOut> UnorderedPipelineSync<TIn, TOut>(
            this IReadPort<TIn> from,
            Func<TIn, TOut> f,
            bool propagateClose = true)
        {
            var parallelism = Environment.ProcessorCount;
            var to = Channel.Create<TOut>(parallelism * 2);

            async Task Pump()
            {
                while (true)
                {
                    var (is_open, job) = await from.Take();
                    if (!is_open) break;
                    try
                    {
                        var putIsOpen = await to.Put(f(job));
                        if (!putIsOpen) return;
                    }
                    catch (Exception ex)
                    {

                    }
                }
            }

            Task.Run(async () =>
            {
                await Task.WhenAll(Enumerable.Range(0, parallelism)
                    .Select(idx => Task.Run(Pump)));

                if (propagateClose)
                {
                    from.Close();
                    to.Close();
                }
            });

            return to;
        }

        public static async Task UnorderedThreadedPipeline<TIn, TOut>(
            this IReadPort<TIn> from,
            int parallelism,
            IWritePort<TOut> to,
            Func<TIn, TOut> f,
            bool propagateClose = true)
        {
            Task Pump()
            {
                var tcs = new TaskCompletionSource<bool>();

                var th = new Thread(() =>
                {
                    while (true)
                    {
                        var (is_open, job) = from.Take().Result;
                        if (!is_open) break;

                        var putIsOpen = to.Put(f(job)).Result;
                        if (!putIsOpen) return;
                    }
                    tcs.SetResult(true);
                }) {Priority = ThreadPriority.BelowNormal};
                th.Start();
                return tcs.Task;
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
