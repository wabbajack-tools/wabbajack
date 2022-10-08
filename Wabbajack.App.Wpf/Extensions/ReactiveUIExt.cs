using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Kernel;
using ReactiveUI;
using Wabbajack.Extensions;

namespace Wabbajack
{
    public static class ReactiveUIExt
    {
        /// <summary>
        /// Convenience function to not have to specify the selector function in the default ReactiveUI WhenAny() call.
        /// Subscribes to changes in a property on a given object.
        /// </summary>
        /// <typeparam name="TSender">Type of object to watch</typeparam>
        /// <typeparam name="TRet">The type of property watched</typeparam>
        /// <param name="This">Object to watch</param>
        /// <param name="property1">Expression path to the property to subscribe to</param>
        /// <returns></returns>
        public static IObservable<TRet?> WhenAny<TSender, TRet>(
            this TSender This,
            Expression<Func<TSender, TRet?>> property1)
            where TSender : class
        {
            return This.WhenAny(property1, selector: x => x.GetValue());
        }

        /// <summary>
        /// Convenience wrapper to observe following calls on the GUI thread.
        /// </summary>
        public static IObservable<T> ObserveOnGuiThread<T>(this IObservable<T> source)
        {
            return source.ObserveOn(RxApp.MainThreadScheduler);
        }

        
        /// <summary>
        /// Like IObservable.Select but supports async map functions
        /// </summary>
        /// <param name="source"></param>
        /// <param name="f"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static IObservable<TOut> SelectAsync<TIn, TOut>(this IObservable<TIn> source, Func<TIn, Task<TOut>> f)
        {
            return source.Select(itm => Observable.FromAsync(async () => await f(itm))).Merge(10);

        }

        public static IObservable<Unit> StartingExecution(this IReactiveCommand cmd)
        {
            return cmd.IsExecuting
                .DistinctUntilChanged()
                .Where(x => x)
                .Unit();
        }

        public static IObservable<Unit> EndingExecution(this IReactiveCommand cmd)
        {
            return cmd.IsExecuting
                .DistinctUntilChanged()
                .Pairwise()
                .Where(x => x.Previous && !x.Current)
                .Unit();
        }

        /// These snippets were provided by RolandPheasant (author of DynamicData)
        /// They'll be going into the official library at some point, but are here for now.
        #region Dynamic Data EnsureUniqueChanges
        /// <summary>
        /// Removes outdated key events from a changeset, only leaving the last relevent change for each key.
        /// </summary>
        public static IObservable<IChangeSet<TObject, TKey>> EnsureUniqueChanges<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
            where TKey : notnull
        {
            return source.Select(EnsureUniqueChanges);
        }

        /// <summary>
        /// Removes outdated key events from a changeset, only leaving the last relevent change for each key.
        /// </summary>
        public static IChangeSet<TObject, TKey> EnsureUniqueChanges<TObject, TKey>(this IChangeSet<TObject, TKey> input)
            where TKey : notnull

        {
            var changes = input
                .GroupBy(kvp => kvp.Key)
                .Select(g => g.Aggregate(Optional<Change<TObject, TKey>>.None, Reduce))
                .Where(x => x.HasValue)
                .Select(x => x.Value);

            return new ChangeSet<TObject, TKey>(changes);
        }

        public static ObservableAsPropertyHelper<TRet> ToGuiProperty<TRet>(
            this IObservable<TRet> source,
            ViewModel vm,
            string property,
            TRet? initialValue = default,
            bool deferSubscription = false)
        {
            return source
                .ToProperty(vm, property, initialValue, deferSubscription, RxApp.MainThreadScheduler)
                .DisposeWith(vm.CompositeDisposable)!;
        }
/*
        public static void ToGuiProperty<TRet>(
            this IObservable<TRet> source,
            ViewModel vm,
            string property,
            out ObservableAsPropertyHelper<TRet> result,
            TRet initialValue = default,
            bool deferSubscription = false)
        {
            
            source.ToProperty(vm, property, out result!, initialValue, deferSubscription, RxApp.MainThreadScheduler)
                .DisposeWith(vm.CompositeDisposable);
        }*/

        internal static Optional<Change<TObject, TKey>> Reduce<TObject, TKey>(Optional<Change<TObject, TKey>> previous, Change<TObject, TKey> next)
            where TKey : notnull

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
