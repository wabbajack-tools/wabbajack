using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Wabbajack.Common;

/// <summary>
///     Represents a cache where values are created on-the-fly when they are found missing.
///     Creating a value locks the cache entry so that each key/value pair is only created once.
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TVal"></typeparam>
public class LazyCache<TKey, TArg, TVal>
where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, AsyncLazy<TVal>> _data;
    private readonly Func<TArg, TKey> _selector;
    private readonly Func<TArg, Task<TVal>> _valueFactory;

    public LazyCache(Func<TArg, TKey> selector, Func<TArg, Task<TVal>> valueFactory)
    {
        _selector = selector;
        _valueFactory = valueFactory;
        _data = new ConcurrentDictionary<TKey, AsyncLazy<TVal>>();
    }

    public async ValueTask<TVal> Get(TArg lookup)
    {
        var key = _selector(lookup);

        while (true)
        {
            if (_data.TryGetValue(key, out var found))
                return await found.Value;

            var value = new AsyncLazy<TVal>(() => _valueFactory(lookup));
            if (_data.TryAdd(key, value))
                return await value.Value;
        }
    }
}