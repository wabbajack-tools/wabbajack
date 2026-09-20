using System;
using System.Text.Json.Serialization;
using Wabbajack.Hashing.xxHash64;

namespace Wabbajack.DTOs;

/// <summary>
///     One file of a game, as one Steam depot manifest publishes it, recorded by the hash Wabbajack
///     identifies it with.
///     <para>
///         This is the answer to a question a version index cannot answer: a modlist records a game file by
///         its xxHash64, and a depot manifest records paths, sizes and Valve's own SHA-1. Only bytes connect
///         the two, so somebody has to fetch the build once and write down what each file hashes to. Having
///         done that, a repair no longer has to know which game version a file belongs to - the hash names
///         the depot and manifest that carry it.
///     </para>
/// </summary>
public class IndexedGameFile
{
    /// <summary>Wabbajack's own hash of the file's contents: what a modlist records a game file by.</summary>
    [JsonPropertyName("Hash")]
    public Hash Hash { get; set; }

    [JsonPropertyName("Size")]
    public long Size { get; set; }

    /// <summary>The path the manifest records, depot-relative, with backslashes.</summary>
    [JsonPropertyName("Path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    ///     The app the depot has to be opened under. Recorded because it is not always the game's own: the
    ///     Creation Kit installs into the game folder from an app of its own, and a depot key is asked for
    ///     per app.
    /// </summary>
    [JsonPropertyName("App")]
    public uint App { get; set; }

    [JsonPropertyName("Depot")]
    public uint Depot { get; set; }

    [JsonPropertyName("Manifest")]
    public ulong Manifest { get; set; }
}

/// <summary>
///     One depot manifest that has been read file by file, and what came of it. Published beside the shards
///     so a later run knows what it can skip and a person can see which builds are covered.
/// </summary>
public class IndexedManifest
{
    [JsonPropertyName("App")]
    public uint App { get; set; }

    [JsonPropertyName("Depot")]
    public uint Depot { get; set; }

    [JsonPropertyName("Manifest")]
    public ulong Manifest { get; set; }

    /// <summary>
    ///     The game version this manifest was published as, when whoever indexed it knew - which is only
    ///     when they indexed a build they had installed. Empty otherwise, and nothing reads it: the hashes
    ///     are what a repair goes by.
    /// </summary>
    [JsonPropertyName("Version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("Files")]
    public int Files { get; set; }

    [JsonPropertyName("Indexed")]
    public DateTime Indexed { get; set; }
}

/// <summary>
///     Where the content index lives and how it is split up. A lookup is one file over HTTP, so the whole
///     thing is sharded by the first byte of the hash: a game with twenty indexed builds is a few megabytes
///     spread over 256 files, and a repair reads only the shards its own files fall in.
/// </summary>
public static class GameFileIndex
{
    /// <summary>The folder inside a game's directory in <c>indexed-game-files</c>.</summary>
    public const string ContentFolder = "content";

    /// <summary>The file recording which manifests have been read, beside the shards.</summary>
    public const string IndexedManifestsFile = "indexed.json";

    /// <summary>
    ///     Which shard a hash falls in: the first byte of it, lower-case hex. Taken from the hex rather
    ///     than the base64 this app usually prints, because a shard is a file name and base64 is not one.
    /// </summary>
    public static string ShardOf(Hash hash)
    {
        return hash.ToHex()[..2].ToLowerInvariant();
    }

    /// <summary>The path of a shard relative to the game's folder in the index.</summary>
    public static string ShardPath(Hash hash)
    {
        return $"{ContentFolder}/{ShardOf(hash)}.json";
    }
}
