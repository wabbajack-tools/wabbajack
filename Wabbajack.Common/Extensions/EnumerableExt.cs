using System;
using System.Collections.Generic;
using System.Linq;

namespace Wabbajack
{
    public static class EnumerableExt
    {
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

        public static IEnumerable<T> Cons<T>(this IEnumerable<T> coll, T next)
        {
            yield return next;
            foreach (var itm in coll) yield return itm;
        }

        /// <summary>
        /// Converts and filters a nullable enumerable to a non-nullable enumerable
        /// </summary>
        public static IEnumerable<T> NotNull<T>(this IEnumerable<T?> enumerable)
            where T : class
        {
            // Filter out nulls
            return enumerable.Where(e => e != null)
                // Cast to non nullable type
                .Select(e => e!);
        }
        
        public static IEnumerable<T> NotNull<T>(this IEnumerable<T?> enumerable) where T : struct
        {
            return enumerable
                .Where(x => x.HasValue)
                .Select(x => x!.Value);
        }

        /// <summary>
        /// Selects items that are castable to the desired type
        /// </summary>
        /// <typeparam name="T">Type of the original enumerable to cast from</typeparam>
        /// <typeparam name="R">Type to attempt casting to</typeparam>
        /// <param name="e">Enumerable to process</param>
        /// <returns>Enumerable with only objects that were castable</returns>
        public static IEnumerable<R> WhereCastable<T, R>(this IEnumerable<T> e)
            where T : class
            where R : T
        {
            return e.Where(e => e is R)
                .Select(e => (R)e);
        }
    }
}
