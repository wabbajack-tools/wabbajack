using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Kernel;
using ReactiveUI;

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
        public static IObservable<TRet> WhenAny<TSender, TRet>(
            this TSender This,
            Expression<Func<TSender, TRet>> property1)
            where TSender : class
        {
            return This.WhenAny(property1, selector: x => x.GetValue());
        }

        /// <summary>
        /// Convenience function that discards events that are null
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns>Source events that are not null</returns>
        public static IObservable<T> NotNull<T>(this IObservable<T> source)
            where T : class
        {
            return source.Where(u => u != null);
        }

        /// <summary>
        /// Convenience wrapper to observe following calls on the GUI thread.
        /// </summary>
        public static IObservable<T> ObserveOnGuiThread<T>(this IObservable<T> source)
        {
            return source.ObserveOn(RxApp.MainThreadScheduler);
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
        public static IObservable<T> FilterSwitch<T>(this IObservable<T> source, IObservable<bool> filterSwitch)
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

        public static IObservable<Unit> StartingExecution(this IReactiveCommand cmd)
        {
            return cmd.IsExecuting
                .DistinctUntilChanged()
                .Where(x => x)
                .Unit();
        }

        /// These snippets were provided by RolandPheasant (author of DynamicData)
        /// They'll be going into the official library at some point, but are here for now.
        #region Dynamic Data EnsureUniqueChanges
        /// <summary>
        /// Removes outdated key events from a changeset, only leaving the last relevent change for each key.
        /// </summary>
        public static IObservable<IChangeSet<TObject, TKey>> EnsureUniqueChanges<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            return source.Select(EnsureUniqueChanges);
        }

        /// <summary>
        /// Removes outdated key events from a changeset, only leaving the last relevent change for each key.
        /// </summary>
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
