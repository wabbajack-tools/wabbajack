namespace Wabbajack.Networking.Steam;

/// <summary>
///     Holds manifest request codes for as long as they are worth reusing.
///     Since 2022 a manifest cannot be fetched from a content server without one of these, and they are
///     short lived. Valve publishes no lifetime; the reference implementation refreshes on a five minute
///     clock and its own comment admits the number is a guess. So this treats the lifetime as an estimate
///     and takes a deliberately shorter one -- a code fetched a second time costs one round trip, while a
///     code that expired mid-download costs the download.
/// </summary>
public sealed class ManifestRequestCodeCache
{
    /// <summary>
    ///     How long a code is reused for. Not a documented value, and not measurable from the outside
    ///     without waiting out an expiry; see the note above.
    /// </summary>
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(4);

    private readonly Dictionary<(uint DepotId, ulong ManifestId, string Branch), Entry> _codes = new();
    private readonly TimeSpan _lifetime;
    private readonly Func<DateTime> _now;
    private readonly object _lock = new();

    public ManifestRequestCodeCache(TimeSpan? lifetime = null, Func<DateTime>? now = null)
    {
        _lifetime = lifetime ?? DefaultLifetime;
        _now = now ?? (() => DateTime.UtcNow);
    }

    /// <summary>The code to use, or null when one has to be asked for.</summary>
    public ulong? Get(uint depotId, ulong manifestId, string branch)
    {
        lock (_lock)
        {
            if (!_codes.TryGetValue((depotId, manifestId, branch), out var entry)) return null;
            if (_now() - entry.FetchedAt >= _lifetime) return null;
            return entry.Code;
        }
    }

    public void Set(uint depotId, ulong manifestId, string branch, ulong code)
    {
        lock (_lock)
        {
            _codes[(depotId, manifestId, branch)] = new Entry(code, _now());
        }
    }

    /// <summary>
    ///     Throws away a code that a content server has just refused, so the next attempt asks Steam for a
    ///     fresh one instead of replaying the dead one.
    /// </summary>
    public void Forget(uint depotId, ulong manifestId, string branch)
    {
        lock (_lock)
        {
            _codes.Remove((depotId, manifestId, branch));
        }
    }

    private readonly record struct Entry(ulong Code, DateTime FetchedAt);
}
