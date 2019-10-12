using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Kernel;
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

        public static IObservable<T> ObserveOnGuiThread<T>(this IObservable<T> source)
        {
            return source.ObserveOn(RxApp.MainThreadScheduler);
        }

        /// These snippets were provided by RolandPheasant (author of DynamicData)
        /// They'll be going into the official library at some point, but are here for now.
        #region Dynamic Data EnsureUniqueChanges        
        public static IObservable<IChangeSet<TObject, TKey>> EnsureUniqueChanges<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            return source.Select(EnsureUniqueChanges);
        }

        public static IChangeSet<TObject, TKey> EnsureUniqueChanges<TObject, TKey>(this IChangeSet<TObject, TKey> input)
        {
            var changes = input
                .GroupBy(kvp => kvp.Key)
                .Select(g => g.Aggregate(Optional<Change<TObject, TKey>>.None, Reduce))
                .Where(x => x.HasValue)
                .Select(x => x.Value);

            return new ChangeSet<TObject, TKey>(changes);
        }


        internal static Optional<Change<TObject, TKey>> Reduce<TObject, TKey>(Optional<Change<TObject, TKey>> previous, Change<TObject, TKey> next)
        {
            if (!previous.HasValue)
            {
                return next;
            }

            var previousValue = previous.Value;

            switch (previousValue.Reason)
            {
                case ChangeReason.Add when next.Reason == ChangeReason.Remove:
                    return Optional<Change<TObject, TKey>>.None;

                case ChangeReason.Remove when next.Reason == ChangeReason.Add:
                    return new Change<TObject, TKey>(ChangeReason.Update, next.Key, next.Current, previousValue.Current, next.CurrentIndex, previousValue.CurrentIndex);

                case ChangeReason.Add when next.Reason == ChangeReason.Update:
                    return new Change<TObject, TKey>(ChangeReason.Add, next.Key, next.Current, next.CurrentIndex);

                case ChangeReason.Update when next.Reason == ChangeReason.Update:
                    return new Change<TObject, TKey>(ChangeReason.Update, previousValue.Key, next.Current, previousValue.Previous, next.CurrentIndex, previousValue.PreviousIndex);

                default:
                    return next;
            }
        }
        #endregion
    }
}
