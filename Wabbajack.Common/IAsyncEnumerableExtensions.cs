using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wabbajack.Common
{
    public static class IAsyncEnumerableExtensions
    {
        /// <summary>
        /// Same as .Select but expects a function that returns an async result
        /// </summary>
        /// <param name="coll"></param>
        /// <param name="mapFn"></param>
        /// <typeparam name="TIn"></typeparam>
        /// <typeparam name="TOut"></typeparam>
        /// <returns></returns>
        public static async IAsyncEnumerable<TOut> SelectAsync<TIn, TOut>(this IEnumerable<TIn> coll,
            Func<TIn, ValueTask<TOut>> mapFn)
        {
            foreach (var itm in coll)
            {
                yield return await mapFn(itm);
            }
        }

        public static async ValueTask<List<T>> ToList<T>(this IAsyncEnumerable<T> coll)
        {
            var list =new List<T>();
            await foreach (var itm in coll)
                list.Add(itm);
            return list;
        }
        
    }
}
