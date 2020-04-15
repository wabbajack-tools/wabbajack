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

        /// <summary>
        /// Adds the given values to the dictionary.  If a key already exists, it will throw an exception
        /// </summary>
        public static void Add<K, V>(this IDictionary<K, V> dict, IEnumerable<KeyValuePair<K, V>> vals)
            where K : notnull
        {
            foreach (var val in vals)
            {
                dict.Add(val);
            }
        }

        /// <summary>
        /// Adds the given values to the dictionary.  If a key already exists, it will be replaced
        /// </summary>
        public static void Set<K, V>(this IDictionary<K, V> dict, IEnumerable<KeyValuePair<K, V>> vals)
            where K : notnull
        {
            foreach (var val in vals)
            {
                dict[val.Key] = val.Value;
            }
        }

        /// <summary>
        /// Clears the dictionary and adds the given values
        /// </summary>
        public static void SetTo<K, V>(this IDictionary<K, V> dict, IEnumerable<KeyValuePair<K, V>> vals)
            where K : notnull
        {
            dict.Clear();
            dict.Set(vals);
        }
    }
}
