using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack.Common.CSP
{
    public static class CSPExtensions
    {
        public static async Task OntoChannel<TIn, TOut>(this IEnumerable<TIn> coll, IChannel<TIn, TOut> chan)
        {
            foreach (var val in coll)
            {
                if (!await chan.Put(val)) break;
            }
        }

        /// <summary>
        /// Turns a IEnumerable collection into a channel. Note, computation of the enumerable will happen inside
        /// the lock of the channel, so try to keep the work of the enumerable light.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="coll">Collection to spool out of the channel.</param>
        /// <returns></returns>
        public static IChannel<T, T> ToChannel<T>(this IEnumerable<T> coll)
        {
            return Channel.Create(coll.GetEnumerator());
        }


        public static async Task<List<TOut>> TakeAll<TOut, TIn>(this IChannel<TIn, TOut> chan)
        {
            List<TOut> acc = new List<TOut>();
            while (true)
            {
                var (open, val) = await chan.Take();
                
                if (!open) break;
                
                acc.Add(val);
            }
            return acc;
        }
    }
}
