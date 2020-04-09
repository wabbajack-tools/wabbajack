using System;
using System.Collections.Generic;

namespace Wabbajack
{
    public static class DictionaryExt
    {
        public static V TryCreate<K, V>(this IDictionary<K, V> dict, K key)
            where K : notnull
            where V : new()
        {
            return dict.TryCreate(key, () => new V());
        }

        public static V TryCreate<K, V>(this IDictionary<K, V> dict, K key, Func<V> create)
            where K : notnull
        {
            if (dict.TryGetValue(key, out var val)) return val;
            var ret = create();
            dict[key] = ret;
            return ret;
        }

        public static void Add<K, V>(this IDictionary<K, V> dict, IEnumerable<KeyValuePair<K, V>> vals)
            where K : notnull
        {
            foreach (var val in vals)
            {
                dict.Add(val);
            }
        }

        public static void Set<K, V>(this IDictionary<K, V> dict, IEnumerable<KeyValuePair<K, V>> vals)
            where K : notnull
        {
            foreach (var val in vals)
            {
                dict[val.Key] = val.Value;
            }
        }
    }
}
