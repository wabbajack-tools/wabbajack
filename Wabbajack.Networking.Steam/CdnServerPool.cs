using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Wabbajack.Networking.Steam;

/// <param name="Key">Whatever identifies this server uniquely, used to strike it off.</param>
/// <param name="Type">Steam's classification. Only <c>SteamCache</c> and <c>CDN</c> serve depot content.</param>
/// <param name="AllowedAppIds">Apps this server will serve. Empty means it serves anything.</param>
/// <param name="NumEntries">How many slots Steam thinks this server is worth in a rotation.</param>
/// <param name="WeightedLoad">How busy Steam says it is. Lower is better.</param>
/// <param name="UseAsProxy">
///     True for the one server that rewrites requests on the way to a real one rather than serving them.
/// </param>
/// <summary>
///     What the pool needs to know about a content server. Stated as a plain record so the pool's decisions
///     do not reach into SteamKit's <c>Server</c>, whose properties cannot be set from outside the library
///     and so cannot be constructed in a test.
/// </summary>
public readonly record struct ContentServerFacts(string Key, string? Type, uint[] AllowedAppIds, int NumEntries,
    float WeightedLoad, bool UseAsProxy);

/// <summary>
///     Hands out SteamPipe content servers, in Steam's own order of preference, and moves past ones that
///     are failing.
///     SteamKit 3.x deliberately ships no pool -- its CDN client takes whichever server you give it -- so
///     something has to decide. Downloading a whole file from a single host is the thing to avoid: content
///     servers fail individually and often, and a run that picked a bad one would fail entirely rather than
///     retry elsewhere.
///     The shape is Valve's: filter the directory to servers that will serve this app, weight them by the
///     number of slots Steam says each is worth, and walk that list. A server that fails is struck off, and
///     when everything has been struck off the directory is fetched again rather than giving up -- a whole
///     region going away is a reason to re-ask Steam, not to stop.
///     <para>
///         The directory itself is not per app. Steam answers with the same servers whatever is asked for,
///         and the app only decides which of them are kept, so one answer is fetched and every app's list
///         is filtered out of it. That distinction is the difference between one round trip and hundreds:
///         a repair alternates between a game's app and its Creation Kit's for every single file, and a
///         pool that threw its list away on each switch asked Steam 150 times for a 75 file repair, every
///         answer the same 24 servers.
///     </para>
/// </summary>
public sealed class CdnServerPool<T>
{
    private const string SteamCacheType = "SteamCache";
    private const string CdnType = "CDN";

    private readonly Func<T, ContentServerFacts> _describe;
    private readonly Func<CancellationToken, Task<IEnumerable<T>>> _fetch;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _rebuild = new(1, 1);

    private readonly HashSet<string> _struckOff = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     The weighted list for each app the pool has been asked about, all filtered out of the same
    ///     <see cref="_directory" />. Read without the rebuild lock, so it is concurrent.
    /// </summary>
    private readonly ConcurrentDictionary<uint, AppSlots> _byApp = new();

    /// <summary>
    ///     Steam's answer as last fetched, before any per-app filter. Only ever touched while
    ///     <see cref="_rebuild" /> is held.
    /// </summary>
    private T[] _directory = Array.Empty<T>();

    public CdnServerPool(ILogger logger, uint? cellId, Func<CancellationToken, Task<IEnumerable<T>>> fetch,
        Func<T, ContentServerFacts> describe)
    {
        _logger = logger;
        CellId = cellId;
        _fetch = fetch;
        _describe = describe;
    }

    /// <summary>
    ///     The server Steam marked <see cref="ContentServerFacts.UseAsProxy" />, if the directory named one.
    ///     It is not a server to download from; it rewrites requests on the way to one, and every CDN call
    ///     takes it alongside the real server.
    /// </summary>
    public T? ProxyServer { get; private set; }

    /// <summary>The cell the pool asks the directory for, only so callers can log what they got.</summary>
    public uint? CellId { get; }

    /// <summary>
    ///     The next server to try. Blocks only while the directory is being fetched.
    /// </summary>
    public async Task<T> TakeAsync(uint appId, CancellationToken token)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (TryNext(appId, out var server)) return server;

            await RebuildAsync(appId, token).ConfigureAwait(false);
        }

        throw new SteamNoContentServersException(
            $"Steam has no content server that will serve app {appId} right now");
    }

    /// <summary>
    ///     One pass of this app's weighted list. Anything struck off since it was built is skipped, and
    ///     running out means everything is struck off, which is what sends the caller round to a rebuild.
    /// </summary>
    private bool TryNext(uint appId, out T server)
    {
        server = default!;

        if (!_byApp.TryGetValue(appId, out var app) || app.Slots.Length == 0) return false;

        for (var i = 0; i < app.Slots.Length; i++)
        {
            var next = app.Slots[(int) ((uint) Interlocked.Increment(ref app.Cursor) % (uint) app.Slots.Length)];

            if (IsStruckOff(next)) continue;

            server = next;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Reports that a server would not serve a request, so the pool stops offering it. Deliberately not
    ///     reversible within a run: a content server that has just refused is not worth coming back to while
    ///     there are others, and the directory is re-fetched once they are all gone.
    /// </summary>
    public void StrikeOff(T server)
    {
        var facts = _describe(server);

        lock (_struckOff)
        {
            if (_struckOff.Add(facts.Key))
                _logger.LogInformation("Dropping Steam content server {Host}, it is not serving requests",
                    facts.Key);
        }
    }

    private bool IsStruckOff(T server)
    {
        lock (_struckOff)
        {
            return _struckOff.Contains(_describe(server).Key);
        }
    }

    private async Task RebuildAsync(uint appId, CancellationToken token)
    {
        await _rebuild.WaitAsync(token).ConfigureAwait(false);
        try
        {
            // Another caller may have rebuilt while this one waited, in which case its list is fine.
            if (_byApp.TryGetValue(appId, out var held) && held.Slots.Any(s => !IsStruckOff(s))) return;

            // Steam is only asked again when there is nothing to filter, or when this app has struck off
            // every server the last answer offered -- the "a whole region went away" case. An app the pool
            // has simply not been asked about before is a filter over the directory already in hand, which
            // is what keeps a repair alternating between a game and its Creation Kit off the wire.
            if (_directory.Length == 0 || held != null)
            {
                _directory = (await _fetch(token).ConfigureAwait(false)).ToArray();
                ProxyServer = _directory.FirstOrDefault(s => _describe(s).UseAsProxy);

                // Both belong to the directory they were made against: the strikes name hosts that may not
                // even be in the new answer, and every app's list was built out of the old one.
                lock (_struckOff)
                {
                    _struckOff.Clear();
                }

                _byApp.Clear();

                _logger.LogInformation("Steam offered {Total} content servers{Proxy}", _directory.Length,
                    ProxyServer == null ? "" : $", proxied through {_describe(ProxyServer).Key}");
            }

            // The proxy is deliberately not a download target. It is passed alongside whichever real server
            // is chosen, and handing it over as the server as well would have it rewrite a request that was
            // already addressed to it.
            var usable = _directory.Where(s => !_describe(s).UseAsProxy && Serves(_describe(s), appId)).ToArray();

            // Ascending weighted load: Steam's number is how busy a server is, so the lightest first.
            var slots = usable
                .Select(s => (Server: s, Facts: _describe(s)))
                .OrderBy(s => s.Facts.WeightedLoad)
                .SelectMany(s => Enumerable.Repeat(s.Server, Math.Max(1, s.Facts.NumEntries)))
                .ToArray();

            _byApp[appId] = new AppSlots(slots);

            // Stands on its own rather than continuing the line above, which is usually not there: after the
            // first app, a list is filtered out of a directory fetched some time ago.
            _logger.LogInformation("{Usable} content servers will serve app {AppId}, {Slots} weighted slots",
                usable.Length, appId, slots.Length);
        }
        finally
        {
            _rebuild.Release();
        }
    }

    private static bool Serves(ContentServerFacts facts, uint appId)
    {
        if (facts.Type != SteamCacheType && facts.Type != CdnType) return false;

        // An empty allow-list means the server takes anything; a populated one is exhaustive.
        return facts.AllowedAppIds.Length == 0 || facts.AllowedAppIds.Contains(appId);
    }

    /// <summary>
    ///     One app's weighted walk over the directory, and how far through it the last caller got. The
    ///     cursor belongs to the list rather than to the pool: two apps handed out in turn would otherwise
    ///     share a position and step through each other's lists.
    /// </summary>
    private sealed class AppSlots
    {
        public readonly T[] Slots;
        public int Cursor = -1;

        public AppSlots(T[] slots)
        {
            Slots = slots;
        }
    }
}
