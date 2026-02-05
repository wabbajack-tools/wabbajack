using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;

namespace Wabbajack.Common;

public static class IEnumerableExtensions
{
    public static void Do<T>(this IEnumerable<T> coll, Action<T> f)
    {
        foreach (var i in coll) f(i);
    }

    public static IEnumerable<TOut> TryKeep<TIn, TOut>(this IEnumerable<TIn> coll, Func<TIn, (bool, TOut)> fn)
    {
        return coll.Select(fn).Where(p => p.Item1).Select(p => p.Item2);
    }

    /// <summary>
    /// Splits the collection into `size` parts
    /// </summary>
    /// <param name="coll"></param>
    /// <param name="count"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static IEnumerable<IEnumerable<T>> Partition<T>(this IEnumerable<T> coll, int count)
    {
        var asList = coll.ToList();

        IEnumerable<T> SkipEnumerable(IList<T> list, int offset, int size)
        {
            for (var i = offset; i < list.Count; i += size)
            {
                yield return list[i];
            }
        }

        return Enumerable.Range(0, count).Select(offset => SkipEnumerable(asList, offset, count));
    }

    /// <summary>
    /// Split the collection into `size` parts
    /// </summary>
    /// <param name="coll"></param>
    /// <param name="size"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> coll, int size)
    {
        List<T> current = new();
        foreach (var itm in coll)
        {
            current.Add(itm);
            if (current.Count == size)
            {
                yield return current;
                current = new List<T>();
            }
        }
        if (current.Count > 0)
            yield return current;
    }


    public static IEnumerable<T> OnEach<T>(this IEnumerable<T> coll, Action<T> fn)
    {
        foreach (var itm in coll)
        {
            fn(itm);
            yield return itm;
        }
    }

    public static async IAsyncEnumerable<T> OnEach<T>(this IEnumerable<T> coll, Func<T, Task> fn)
    {
        foreach (var itm in coll)
        {
            await fn(itm);
            yield return itm;
        }
    }
}