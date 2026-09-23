using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.CLI;

/// <summary>
///     The content index as it sits in a clone of <c>wabbajack-tools/indexed-game-files</c>: one folder per
///     game holding <c>steam_depots/{xx}.json</c> shards of <see cref="IndexedGameFile" />, and
///     <c>steam_depots/_indexed.json</c> saying which manifests have been read.
///     <para>
///         Everything here merges rather than replaces. The index is a repository several people add to,
///         one build at a time, over years; a run that rewrote a shard from what it happened to fetch today
///         would delete every older build's files from it. So a shard is loaded, added to, and written
///         back, and an entry is keyed by the manifest and path it came from - the same file in two builds
///         is two entries with one hash, which is exactly what makes the index useful.
///     </para>
///     <para>
///         Reading every shard on the way in is deliberate too: it is a few hundred kilobytes, and it is
///         what lets a re-run skip the files it already has rather than download a game again to find out
///         it knew them.
///     </para>
/// </summary>
internal sealed class GameFileIndexFolder
{
    private readonly DTOSerializer _dtos;
    private readonly AbsolutePath _folder;

    /// <summary>Manifests that have been read all the way through, by app, depot and manifest.</summary>
    private readonly Dictionary<(uint App, uint Depot, ulong Manifest), IndexedManifest> _manifests = new();

    /// <summary>Shard name to the files in it, keyed by where each file came from.</summary>
    private readonly Dictionary<string, Dictionary<string, IndexedGameFile>> _shards = new();

    /// <summary>Shards this run has added to, so the rest are not rewritten.</summary>
    private readonly HashSet<string> _touched = new();

    /// <summary>
    ///     Every file the index holds, by where it came from. Kept beside the shards because the question a
    ///     run asks is "have I hashed this path of this manifest", and the shard an answer lives in follows
    ///     from the hash - which is the one thing not known until the file has been fetched.
    /// </summary>
    private readonly HashSet<string> _keys = new();

    /// <summary>
    ///     What each Valve SHA-1 the index has seen hashed to. Every build of a game shares most of its
    ///     files with the one before it, so this is nearly all of a second build.
    /// </summary>
    private readonly Dictionary<string, Hash> _bySha1 = new(StringComparer.OrdinalIgnoreCase);

    private GameFileIndexFolder(AbsolutePath folder, DTOSerializer dtos)
    {
        _folder = folder;
        _dtos = dtos;
    }

    /// <summary>Every manifest that has been read, for a caller reporting coverage.</summary>
    public IReadOnlyCollection<IndexedManifest> Manifests => _manifests.Values;

    public int FileCount => _shards.Values.Sum(s => s.Count);

    /// <summary>
    ///     The xxHash64 of a file whose Valve SHA-1 is already in the index, or null. Two files with the
    ///     same SHA-1 are the same bytes, and a manifest states it before anything is fetched, so this is
    ///     what stops a second build of a game being a second download of it: only what actually changed
    ///     has to be read.
    /// </summary>
    public Hash? KnownBySha1(string sha1)
    {
        return string.IsNullOrWhiteSpace(sha1) ? null :
            _bySha1.TryGetValue(sha1, out var hash) ? hash : null;
    }

    /// <summary>
    ///     Loads what the index already holds for a game. A folder that is not there yet is an empty index,
    ///     not an error: the first person to index a game creates it.
    /// </summary>
    public static async Task<GameFileIndexFolder> Load(AbsolutePath root, Game game, DTOSerializer dtos,
        CancellationToken token)
    {
        var folder = new GameFileIndexFolder(root.Combine(game.ToString()), dtos);
        await folder.Read(token);
        return folder;
    }

    /// <summary>Whether this manifest has already been read all the way through.</summary>
    public bool HasManifest(uint app, uint depot, ulong manifest)
    {
        return _manifests.ContainsKey((app, depot, manifest));
    }

    /// <summary>
    ///     The depot and manifest ids somebody recorded for a version of this game, out of the index's own
    ///     <c>{version}_steam_manifests.json</c>, or empty when nobody has.
    ///     <para>
    ///         This is how a build gets content-indexed by someone who does not have it installed: the ids
    ///         were written down by whoever did, years ago, and Steam will still serve a manifest to anyone
    ///         who can name it. It is the only reason an old build is reachable at all.
    ///     </para>
    /// </summary>
    public async Task<SteamManifest[]> RecordedManifests(string version, CancellationToken token)
    {
        var file = _folder.Combine(GameFileIndex.SteamManifestsFile(version));
        return file.FileExists() ? await Read<SteamManifest>(file, token) : Array.Empty<SteamManifest>();
    }

    /// <summary>
    ///     Whether this exact file of this exact manifest is already recorded. Asked per file so a run that
    ///     was interrupted half way through a manifest picks up where it stopped: the shards are written as
    ///     it goes, and they are the truth about what has been hashed.
    /// </summary>
    public bool Has(uint app, uint depot, ulong manifest, string path)
    {
        return _keys.Contains(KeyOf(app, depot, manifest, path));
    }

    public void Add(IndexedGameFile file)
    {
        var shard = GameFileIndex.ShardOf(file.Hash);
        if (!_shards.TryGetValue(shard, out var entries))
            _shards[shard] = entries = new Dictionary<string, IndexedGameFile>();

        var key = KeyOf(file.App, file.Depot, file.Manifest, file.Path);
        entries[key] = file;
        _keys.Add(key);
        if (!string.IsNullOrWhiteSpace(file.Sha1)) _bySha1[file.Sha1] = file.Hash;
        _touched.Add(shard);
    }

    /// <summary>Records a manifest as read. Re-reading one replaces what was said about it.</summary>
    public void RecordManifest(IndexedManifest manifest)
    {
        _manifests[(manifest.App, manifest.Depot, manifest.Manifest)] = manifest;
    }

    /// <summary>
    ///     Writes the shards this run touched, and the manifest record. Called after each manifest rather
    ///     than once at the end: indexing a game is hours of downloading, and a run that is stopped half way
    ///     should leave behind everything it did up to then.
    /// </summary>
    public async Task Save(CancellationToken token)
    {
        var content = _folder.Combine(GameFileIndex.ContentFolder);
        content.CreateDirectory();

        foreach (var shard in _touched.OrderBy(s => s, StringComparer.Ordinal))
        {
            var entries = _shards[shard].Values
                .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                .ThenBy(f => f.Depot)
                .ThenBy(f => f.Manifest)
                .ToArray();

            await Write(content.Combine($"{shard}.json"), entries, token);
        }

        _touched.Clear();

        var manifests = _manifests.Values
            .OrderBy(m => m.App).ThenBy(m => m.Depot).ThenBy(m => m.Manifest)
            .ToArray();

        await Write(content.Combine(GameFileIndex.IndexedManifestsFile), manifests, token);
    }

    private static string KeyOf(uint app, uint depot, ulong manifest, string path)
    {
        return $"{app}|{depot}|{manifest}|{path.ToLowerInvariant()}";
    }

    private async Task Read(CancellationToken token)
    {
        var content = _folder.Combine(GameFileIndex.ContentFolder);
        if (!content.DirectoryExists()) return;

        foreach (var file in content.EnumerateFiles(Ext.Json))
        {
            if (file.FileName == (RelativePath) GameFileIndex.IndexedManifestsFile)
            {
                foreach (var manifest in await Read<IndexedManifest>(file, token))
                    _manifests[(manifest.App, manifest.Depot, manifest.Manifest)] = manifest;
                continue;
            }

            var shard = file.FileName.WithoutExtension().ToString().ToLowerInvariant();
            var entries = new Dictionary<string, IndexedGameFile>();
            foreach (var indexed in await Read<IndexedGameFile>(file, token))
            {
                var key = KeyOf(indexed.App, indexed.Depot, indexed.Manifest, indexed.Path);
                entries[key] = indexed;
                _keys.Add(key);
                if (!string.IsNullOrWhiteSpace(indexed.Sha1)) _bySha1[indexed.Sha1] = indexed.Hash;
            }

            _shards[shard] = entries;
        }
    }

    private async Task<T[]> Read<T>(AbsolutePath file, CancellationToken token)
    {
        await using var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        return await _dtos.DeserializeAsync<T[]>(stream, token) ?? Array.Empty<T>();
    }

    private async Task Write<T>(AbsolutePath file, T[] value, CancellationToken token)
    {
        await using var stream = file.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        await _dtos.Serialize(value, stream, writeIndented: true);
    }
}
