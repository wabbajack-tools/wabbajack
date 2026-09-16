namespace Wabbajack.Networking.Steam;

/// <summary>
///     Holds a bounded number of things read out of depot manifests, keyed by depot, manifest and branch,
///     dropping the least recently used once there are too many.
///     A manifest id names one immutable build, so anything derived from a manifest is worth keeping for as
///     long as it is being used: a repair reads the same manifest once per file to find it and again to
///     fetch it, and re-reading costs the whole manifest each time. What it is not worth is keeping
///     forever. The thing doing the caching lives as long as its host; unbounded is tolerable in a command
///     that exits and a growing floor under a desktop session that does not.
///     Least-recently-used rather than oldest-first because the access pattern is a search: several
///     manifests are read to find which one has a file, and then the one that answered is read again.
///     The capacity is the caller's to choose and it is not a free dial. The access pattern above sweeps a
///     whole set of manifests per file, so a capacity below the size of that set does not merely halve the
///     hit rate -- it drives it to zero, every entry being evicted just before it is wanted again, and the
///     cache then costs a manifest download per file rather than saving one. Size it above the working set
///     and let memory be the only thing the bound is arguing with.
/// </summary>
public sealed class ManifestCache<T> where T : class
{
    private readonly int _capacity;
    private readonly Dictionary<Key, T> _entries = new();
    private readonly object _lock = new();

    /// <summary>Keys in use order, least recent first.</summary>
    private readonly List<Key> _order = new();

    public ManifestCache(int capacity)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity), "A cache of nothing is not one");
        _capacity = capacity;
    }

    /// <summary>How many entries are held. For tests and for logging; not something to branch on.</summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>What is held for this manifest, or null. A hit counts as a use.</summary>
    public T? Get(uint depotId, ulong manifestId, string branch)
    {
        var key = new Key(depotId, manifestId, branch);
        lock (_lock)
        {
            if (!_entries.TryGetValue(key, out var value)) return null;
            _order.Remove(key);
            _order.Add(key);
            return value;
        }
    }

    /// <summary>
    ///     Keeps a value, evicting the least recently used if that takes it over capacity. Setting a key
    ///     that is already held leaves the held value alone and counts as a use: two callers racing on the
    ///     same manifest have each read the same immutable build, so the second answer is redundant rather
    ///     than newer, and replacing it would hand the two of them different objects for the same thing.
    /// </summary>
    public void Set(uint depotId, ulong manifestId, string branch, T value)
    {
        var key = new Key(depotId, manifestId, branch);
        lock (_lock)
        {
            if (_entries.ContainsKey(key))
            {
                _order.Remove(key);
                _order.Add(key);
                return;
            }

            _entries[key] = value;
            _order.Add(key);

            while (_order.Count > _capacity)
            {
                _entries.Remove(_order[0]);
                _order.RemoveAt(0);
            }
        }
    }

    private readonly record struct Key(uint DepotId, ulong ManifestId, string Branch);
}
