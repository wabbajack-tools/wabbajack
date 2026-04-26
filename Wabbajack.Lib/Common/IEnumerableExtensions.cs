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

    #region Shuffle

    /// https://stackoverflow.com/questions/5807128/an-extension-method-on-ienumerable-needed-for-shuffling

    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source, Random rng)
    {
        return source.ShuffleIterator(rng);
    }

    private static IEnumerable<T> ShuffleIterator<T>(
        this IEnumerable<T> source, Random rng)
    {
        var buffer = source.ToList();
        for (int i = 0; i < buffer.Count; i++)
        {
            int j = rng.Next(i, buffer.Count);
            yield return buffer[j];

            buffer[j] = buffer[i];
        }
    }

    #endregion


    public static IEnumerable<TOut> TryKeep<TIn, TOut>(this IEnumerable<TIn> coll, Func<TIn, (bool, TOut)> fn)
    {
        return coll.Select(fn).Where(p => p.Item1).Select(p => p.Item2);
    }

    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> coll)
    {
        var rnd = new Random();
        var data = coll.ToArray();
        for (var x = 0; x < data.Length; x++)
        {
            var a = rnd.Next(0, data.Length);
            var b = rnd.Next(0, data.Length);

            (data[b], data[a]) = (data[a], data[b]);
        }

        return data;
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