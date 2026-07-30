using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Wabbajack.Extensions
{
    public static class RxExt
    {
        /// <summary>
        /// Convenience function that discards events that are null
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns>Source events that are not null</returns>
        public static IObservable<T> NotNull<T>(this IObservable<T?> source)
            where T : class
        {
            return source
                .Where(u => u != null)
                .Select(u => u!);
        }

        /// <summary>
        /// Converts any observable to type Unit.  Useful for when you care that a signal occurred,
        /// but don't care about what its value is downstream.
        /// </summary>
        /// <returns>An observable that returns Unit anytime the source signal fires an event.</returns>
        public static IObservable<Unit> Unit<T>(this IObservable<T> source)
        {
            return source.Select(_ => System.Reactive.Unit.Default);
        }

        /// <summary>
        /// Convenience operator to subscribe to the source observable, only when a second "switch" observable is on.
        /// When the switch is on, the source will be subscribed to, and its updates passed through.
        /// When the switch is off, the subscription to the source observable will be stopped, and no signal will be published.
        /// </summary>
        /// <param name="source">Source observable to subscribe to if on</param>
        /// <param name="filterSwitch">On/Off signal of whether to subscribe to source observable</param>
        /// <returns>Observable that publishes data from source, if the switch is on.</returns>
        public static IObservable<T> FlowSwitch<T>(this IObservable<T> source, IObservable<bool> filterSwitch)
        {
            return filterSwitch
                .DistinctUntilChanged()
                .Select(on =>
                {
                    if (on)
                    {
                        return source;
                    }
                    else
                    {
                        return Observable.Empty<T>();
                    }
                })
                .Switch();
        }

        /// <summary>
        /// Convenience operator to subscribe to the source observable, only when a second "switch" observable is on.
        /// When the switch is on, the source will be subscribed to, and its updates passed through.
        /// When the switch is off, the subscription to the source observable will be stopped, and no signal will be published.
        /// </summary>
        public static IObservable<T> FlowSwitch<T>(this IObservable<T> source, IObservable<bool> filterSwitch, T valueWhenOff)
        {
            return filterSwitch
                .DistinctUntilChanged()
                .Select(on =>
                {
                    if (on)
                    {
                        return source;
                    }
                    else
                    {
                        return Observable.Return<T>(valueWhenOff);
                    }
                })
                .Switch();
        }

        /// Inspiration:
        /// http://reactivex.io/documentation/operators/debounce.html
        /// https://stackoverflow.com/questions/20034476/how-can-i-use-reactive-extensions-to-throttle-events-using-a-max-window-size
        public static IObservable<T> Debounce<T>(this IObservable<T> source, TimeSpan interval, IScheduler? scheduler = null)
        {
            scheduler ??= Scheduler.Default;
            return Observable.Create<T>(o =>
            {
                var hasValue = false;
                bool throttling = false;
                T? value = default;

                var dueTimeDisposable = new SerialDisposable();

                void internalCallback()
                {
                    if (hasValue)
                    {
                        // We have another value that came in to fire.
                        // Reregister for callback
                        dueTimeDisposable.Disposable = scheduler!.Schedule(interval, internalCallback);
                        o.OnNext(value!);
                        value = default;
                        hasValue = false;
                    }
                    else
                    {
                        // Nothing to do, throttle is complete.
                        throttling = false;
                    }
                }

                return source.Subscribe(
                    onNext: (x) =>
                    {
                        if (!throttling)
                        {
                            // Fire initial value
                            o.OnNext(x);
                            // Mark that we're throttling
                            throttling = true;
                            // Register for callback when throttle is complete
                            dueTimeDisposable.Disposable = scheduler.Schedule(interval, internalCallback);
                        }
                        else
                        {
                            // In the middle of throttle
                            // Save value and return
                            hasValue = true;
                            value = x;
                        }
                    },
                    onError: o.OnError,
                    onCompleted: o.OnCompleted);
            });
        }

        public static IObservable<Unit> SelectTask<T>(this IObservable<T> source, Func<T, Task> task)
        {
            return source
                .SelectMany(async i =>
                {
                    await task(i).ConfigureAwait(false);
                    return System.Reactive.Unit.Default;
                });
        }

        public static IObservable<Unit> SelectTask<T>(this IObservable<T> source, Func<Task> task)
        {
            return source
                .SelectMany(async _ =>
                {
                    await task().ConfigureAwait(false);
                    return System.Reactive.Unit.Default;
                });
        }

        public static IObservable<R> SelectTask<T, R>(this IObservable<T> source, Func<Task<R>> task)
        {
            return source
                .SelectMany(_ => task());
        }

        public static IObservable<R> SelectTask<T, R>(this IObservable<T> source, Func<T, Task<R>> task)
        {
            return source
                .SelectMany(x => task(x));
        }

        public static IObservable<T> DoTask<T>(this IObservable<T> source, Func<T, Task> task)
        {
            return source
                .SelectMany(async (x) =>
                {
                    await task(x).ConfigureAwait(false);
                    return x;
                });
        }

        public static IObservable<R> WhereCastable<T, R>(this IObservable<T> source)
            where R : class
            where T : class
        {
            return source
                .Select(x => x as R)
                .NotNull();
        }

        public static IObservable<bool> Invert(this IObservable<bool> source)
        {
            return source.Select(x => !x);
        }

        public static IObservable<(T Previous, T Current)> Pairwise<T>(this IObservable<T> source)
        {
            T? prevStorage = default;
            return source.Select(i =>
            {
                var prev = prevStorage;
                prevStorage = i;
                return (prev, i);
            })!;
        }

        public static IObservable<T> DelayInitial<T>(this IObservable<T> source, TimeSpan delay, IScheduler scheduler)
        {
            return source.FlowSwitch(
                Observable.Return(System.Reactive.Unit.Default)
                    .Delay(delay, scheduler)
                    .Select(_ => true)
                    .StartWith(false));
        }

        public static IObservable<T?> DisposeOld<T>(this IObservable<T?> source)
            where T : class, IDisposable
        {
            return source
                .StartWith(default(T))
                .Pairwise()
                .Do(x =>
                {
                    if (x.Previous != null)
                    {
                        x.Previous.Dispose();
                    }
                })
                .Select(x => x.Current);
        }
    }
}
