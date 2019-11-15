using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Wabbajack.VirtualFileSystem
{
    public static class Extensions
    {
        public static ImmutableDictionary<TK, TI> ToImmutableDictionary<TI, TK>(this IEnumerable<TI> coll,
            Func<TI, TK> keyFunc)
        {
            var builder = ImmutableDictionary<TK, TI>.Empty.ToBuilder();
            foreach (var itm in coll)
                builder.Add(keyFunc(itm), itm);
            return builder.ToImmutable();
        }

        public static ImmutableDictionary<TK, ImmutableStack<TI>> ToGroupedImmutableDictionary<TI, TK>(
            this IEnumerable<TI> coll, Func<TI, TK> keyFunc)
        {
            var builder = ImmutableDictionary<TK, ImmutableStack<TI>>.Empty.ToBuilder();
            foreach (var itm in coll)
            {
                var key = keyFunc(itm);
                if (builder.TryGetValue(key, out var prev))
                    builder[key] = prev.Push(itm);
                else
                    builder[key] = ImmutableStack<TI>.Empty.Push(itm);
            }

            return builder.ToImmutable();
        }
    }
}