using System;
using System.Linq.Expressions;
using System.Reactive.Linq;
using ReactiveUI;

namespace Wabbajack
{
    public static class ReactiveUIExt
    {
        public static IObservable<TRet> WhenAny<TSender, TRet>(
            this TSender This,
            Expression<Func<TSender, TRet>> property1)
            where TSender : class
        {
            return This.WhenAny(property1, selector: x => x.GetValue());
        }

        public static IObservable<T> NotNull<T>(this IObservable<T> source)
            where T : class
        {
            return source.Where(u => u != null);
        }
    }
}
