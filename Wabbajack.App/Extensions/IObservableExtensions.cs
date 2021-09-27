using System;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace Wabbajack.App.Extensions
{
    public static class IObservableExtensions
    {
        public static IObservable<TOut> SelectAsync<TIn, TOut>(this IObservable<TIn> input,
            CompositeDisposable disposable,
            Func<TIn, ValueTask<TOut>> func)
        {
            Subject<TOut> returnObs = new();

            input.Subscribe(x => Task.Run(async () =>
            {
                var result = await func(x);
                returnObs.OnNext(result);
            })).DisposeWith(disposable);
            
            return returnObs;
        }

    }
}