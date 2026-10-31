using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Networking.Bethesda;

/// <summary>One Creation, fetched and unpacked, with both of its files on disk.</summary>
/// <param name="ContentId">The Creation this is.</param>
/// <param name="Title">What Bethesda calls it, when the record said. For messages.</param>
/// <param name="Folder">Where the files were written.</param>
/// <param name="Files">What came out of the container, leaf names relative to <paramref name="Folder" />.</param>
public sealed record UnpackedCreation(long ContentId, string? Title, AbsolutePath Folder,
    IReadOnlyList<RelativePath> Files)
{
    /// <summary>
    ///     The unpacked file that answers a wanted game file, by leaf name. A modlist asks for
    ///     <c>Data\ccBGSSSE002-ExoticArrows.esl</c>; the container wrote the leaf, and both are compared
    ///     ordinal-ignore-case because neither side is consistent about case.
    /// </summary>
    public AbsolutePath? Find(RelativePath gameFile)
    {
        var wanted = gameFile.Parts is null or {Length: 0} ? null : gameFile.Parts[^1];
        if (string.IsNullOrEmpty(wanted)) return null;

        foreach (var file in Files)
            if (string.Equals(file.ToString(), wanted, StringComparison.OrdinalIgnoreCase))
                return Folder.Combine(file);

        return null;
    }

    /// <summary>The unpacked file names, for a message that has to say what the Creation did contain.</summary>
    public string Describe()
    {
        return string.Join(", ", Files.Select(f => f.ToString()));
    }
}

/// <summary>
///     The Creations fetched during one run, unpacked and kept.
///     <para>
///         This is not an optimisation, it is what makes the feature work at all. One <c>.ckm</c> unpacks to
///         <em>two</em> files - a plugin and its archive - and <see cref="IGameFileRestorer.Restore" /> is
///         asked for one file at a time. Without somewhere to keep the other one, every Creation in a repair
///         is downloaded twice: tens of megabytes each, and for a list that wants the whole Anniversary
///         Edition set, twice over seventy-odd times.
///     </para>
///     <para>
///         The signing in and the resolving are kept here for the same reason. The ticket costs a child
///         process and a round trip to the Steam client, and the resolve is one request that answers for
///         every Creation there is, so both happen once for the run and the result is held.
///     </para>
/// </summary>
public sealed class CreationCache : IDisposable
{
    /// <summary>
    ///     How many unpacked Creations are kept at once: all of them.
    ///     <para>
    ///         The bound has to clear a whole repair's working set, and the working set here is however many
    ///         distinct Creations that repair touches - at most the whole table, because a list can want
    ///         every Creation there is. A bound below that evicts the entry that is about to be asked for
    ///         again, and with two files per Creation the eviction lands exactly between them. That failure
    ///         has already happened once in this tree: the Steam manifest cache was bounded at four while a
    ///         repair swept thirteen manifests, so the hit rate was zero and thirteen distinct manifests cost
    ///         973 downloads. The margin is not worth trimming.
    ///     </para>
    ///     <para>
    ///         What the bound protects is disk rather than memory: an entry is a folder of unpacked files
    ///         under the run's <see cref="TemporaryFileManager" />, which goes when the run does. For a
    ///         whole-table repair that is roughly the same bytes the repair is placing in the downloads
    ///         folder anyway, held for as long as the run - which is the price of never fetching a Creation
    ///         twice.
    ///     </para>
    /// </summary>
    public const int MaxCachedCreations = CreationIndex.ExpectedCount;

    private readonly BethesdaApiClient _api;
    private readonly HttpClient _client;
    private readonly Dictionary<long, CachedCreation> _entries = new();
    private readonly CreationIndex _index;

    /// <summary>
    ///     One caller at a time, held across a whole fetch. Repairs run one file after another - the same
    ///     reason <c>GameFileRepair</c> does not use <c>PMapAll</c> - so this costs nothing, and it is what
    ///     stops two callers asking for the same Creation from both downloading it.
    /// </summary>
    private readonly SemaphoreSlim _lock = new(1, 1);

    private readonly ILogger<CreationCache> _logger;

    /// <summary>Content ids, least recently used last. Only ever walked when something has to go.</summary>
    private readonly LinkedList<long> _recent = new();

    private readonly TemporaryFileManager _temp;

    private bool _disposed;
    private IReadOnlyDictionary<long, CkmSlot>? _slots;

    public CreationCache(ILogger<CreationCache> logger, BethesdaApiClient api, CreationIndex index,
        HttpClient client, TemporaryFileManager temp)
    {
        _logger = logger;
        _api = api;
        _index = index;
        _client = client;
        _temp = temp;
    }

    /// <summary>How many Creations are currently unpacked and held. For tests and for a log line.</summary>
    public int Count
    {
        get
        {
            lock (_entries) return _entries.Count;
        }
    }

    /// <summary>
    ///     How many <c>.ckm</c> containers this has actually downloaded. The number a cache exists to keep
    ///     down, so it is worth being able to assert on.
    /// </summary>
    public int Downloads { get; private set; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_entries)
        {
            foreach (var entry in _entries.Values) entry.Dispose();
            _entries.Clear();
            _recent.Clear();
        }

        _lock.Dispose();
    }

    /// <summary>
    ///     The unpacked Creation for a content id, fetching it if this run has not already.
    ///     Null means Bethesda resolved no downloadable record for that id, which is the caller's to
    ///     interpret: it is what a Creation the account does not own is expected to look like, and it is
    ///     equally what an id Bethesda has stopped publishing looks like. Nothing here infers ownership from
    ///     it.
    /// </summary>
    public async Task<UnpackedCreation?> Get(long contentId, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (TryGetCached(contentId, out var hit)) return hit;

        await _lock.WaitAsync(token);
        try
        {
            // Asked again under the lock: another caller may have fetched it while this one was waiting.
            if (TryGetCached(contentId, out hit)) return hit;

            var slot = (await Slots(token)).GetValueOrDefault(contentId);
            if (slot == null) return null;

            var unpacked = await Fetch(slot, token);
            Store(contentId, unpacked);
            return unpacked.Creation;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    ///     The best <c>.ckm</c> per content id, for every Creation in the table, resolved once. One request
    ///     answers for all of them, so asking per file would be the same call seventy-four times over.
    /// </summary>
    private async Task<IReadOnlyDictionary<long, CkmSlot>> Slots(CancellationToken token)
    {
        if (_slots != null) return _slots;

        if (!_api.HasSession)
        {
            await _api.SignIn(token);
            _logger.LogInformation("Bethesda session established for this run");
        }

        var resolved = await _api.Resolve(_index.ContentIds, token);
        _slots = resolved.ToDictionary(s => s.ContentId);

        _logger.LogInformation("Bethesda resolved {Resolved} of {Asked} Creations to a download",
            _slots.Count, _index.ContentIds.Count);

        return _slots;
    }

    /// <summary>
    ///     Fetches one container, checks it against the checksum the record carried, and unpacks it into a
    ///     folder of its own.
    ///     <para>
    ///         The container is held in memory to be unpacked, because that is the shape
    ///         <see cref="BtarArchive" /> reads and a BTAR has no index to seek by. The largest Creation is
    ///         some hundreds of megabytes, held for as long as it takes to write its two files out.
    ///     </para>
    /// </summary>
    private async Task<CachedCreation> Fetch(CkmSlot slot, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, slot.Url);
        BethesdaHeaders.ApplyDownload(request);

        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (!response.IsSuccessStatusCode)
            throw new BethesdaApiException(
                $"Fetching the Creation container for {Describe(slot)} answered {(int) response.StatusCode}.",
                response.StatusCode);

        var container = await response.Content.ReadAsByteArrayAsync(token);
        Downloads++;

        VerifyChecksum(slot, container);

        var folder = _temp.CreateFolder();
        try
        {
            var files = await BtarArchive.ExtractTo(container, folder.Path, token);
            _logger.LogInformation("Unpacked {Creation} ({Bytes} bytes) into {Count} file(s)", Describe(slot),
                container.Length, files.Count);

            return new CachedCreation(new UnpackedCreation(slot.ContentId, slot.Title, folder.Path, files), folder);
        }
        catch
        {
            folder.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     The record's MD5 over the container, when the record carried a real one.
    ///     <para>
    ///         This is the only thing that says the bytes are the ones Bethesda meant to serve, and it is
    ///         deliberately all it says. Whether they are the bytes <em>this modlist</em> wanted is
    ///         <c>Archive.Hash</c>'s question, asked by <c>GameFileRepair</c> on what gets written out, and
    ///         nothing here duplicates or substitutes for it.
    ///     </para>
    ///     <para>
    ///         A slot whose checksum is a multipart etag rather than an MD5 is not checked here, because
    ///         there is nothing to check it against - <see cref="CkmSlot.ChecksumIsMd5" /> is where that is
    ///         decided, and <see cref="CkmDownload" /> prefers a slot that has a real one.
    ///     </para>
    /// </summary>
    private void VerifyChecksum(CkmSlot slot, byte[] container)
    {
        if (!slot.ChecksumIsMd5)
        {
            _logger.LogWarning("{Creation} came with no usable checksum, so the container could not be verified",
                Describe(slot));
            return;
        }

        var actual = Convert.ToHexString(MD5.HashData(container)).ToLowerInvariant();
        if (actual == slot.Checksum) return;

        throw new InvalidDataException(
            $"The Creation container for {Describe(slot)} hashes to {actual}, not the {slot.Checksum} " +
            "Bethesda's record says it should. It has not been unpacked.");
    }

    private bool TryGetCached(long contentId, out UnpackedCreation? creation)
    {
        lock (_entries)
        {
            if (_entries.TryGetValue(contentId, out var entry))
            {
                _recent.Remove(contentId);
                _recent.AddFirst(contentId);
                creation = entry.Creation;
                return true;
            }
        }

        creation = null;
        return false;
    }

    private void Store(long contentId, CachedCreation entry)
    {
        List<CachedCreation> evicted = new();

        lock (_entries)
        {
            _entries[contentId] = entry;
            _recent.Remove(contentId);
            _recent.AddFirst(contentId);

            while (_entries.Count > MaxCachedCreations && _recent.Last != null)
            {
                var oldest = _recent.Last.Value;
                _recent.RemoveLast();
                if (_entries.Remove(oldest, out var gone)) evicted.Add(gone);
            }
        }

        // Outside the lock: deleting a folder is filesystem work, and nothing else needs to wait for it.
        foreach (var gone in evicted)
        {
            _logger.LogDebug("Evicting cached Creation {ContentId}", gone.Creation.ContentId);
            gone.Dispose();
        }
    }

    private static string Describe(CkmSlot slot)
    {
        return slot.Title is {Length: > 0} title ? $"{title} ({slot.ContentId})" : slot.ContentId.ToString();
    }

    /// <summary>An unpacked Creation and the temporary folder that holds it, so eviction can delete it.</summary>
    private sealed record CachedCreation(UnpackedCreation Creation, TemporaryPath Folder) : IDisposable
    {
        public void Dispose()
        {
            Folder.Dispose();
        }
    }
}
