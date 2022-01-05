using System;
using System.Collections.Generic;
using System.Linq;
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