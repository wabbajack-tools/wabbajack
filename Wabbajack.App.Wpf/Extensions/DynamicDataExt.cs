using System;
using System.Linq;
using System.Reactive.Linq;
using DynamicData;

namespace Wabbajack
{
    public static class DynamicDataExt
    {
        public static IObservable<int> CollectionCount<TObject>(this IObservable<IChangeSet<TObject>> source)
        {
            int count = 0;
            return source
                .Select(changeSet =>
                {
                    count += changeSet.Adds;
                    count -= changeSet.Removes;
                    return count;
                })
                .StartWith(0);
        }

        public static IObservable<int> CollectionCount<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            int count = 0;
            return source
                .Select(changeSet =>
                {
                    count += changeSet.Adds;
                    count -= changeSet.Removes;
                    return count;
                })
                .StartWith(0);
        }

        public static IObservable<IChangeSet<TCache, TKey>> TransformAndCache<TObject, TKey, TCache>(
            this IObservable<IChangeSet<TObject, TKey>> obs,
            Func<TKey, TObject, TCache> onAdded,
            Action<Change<TObject, TKey>, TCache> onUpdated)
        {
            var cache = new ChangeAwareCache<TCache, TKey>();
            return obs
                .Select(changeSet =>
                {
                    foreach (var change in changeSet)
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                            case ChangeReason.Update:
                            case ChangeReason.Refresh:
                                var lookup = cache.Lookup(change.Key);
                                TCache val;
                                if (lookup.HasValue)
                                {
                                    val = lookup.Value;
                                }
                                else
                                {
                                    val = onAdded(change.Key, change.Current);
                                    cache.Add(val, change.Key);
                                }
                                onUpdated(change, val);
                                break;
                            case ChangeReason.Remove:
                                cache.Remove(change.Key);
                                break;
                            case ChangeReason.Moved:
                                break;
                            default:
                                throw new NotImplementedException();
                        }
                    }
                    return cache.CaptureChanges();
                })
                .Where(cs => cs.Count > 0);
        }
    }
}
