using System.Collections.Generic;

namespace Wabbajack.Common
{
    public static partial class Utils
    {
        public static IEnumerable<T> Cons<T>(this IEnumerable<T> coll, T next)
        {
            yield return next;
            foreach (var itm in coll) yield return itm;
        }
        
    }
}
