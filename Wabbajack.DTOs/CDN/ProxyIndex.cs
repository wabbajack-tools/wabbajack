using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Wabbajack.Hashing.xxHash64;

namespace Wabbajack.DTOs.CDN;

/// <summary>
///     The proxy's <c>index.json</c>: every file it holds, by hash, with its size. A proxied file is stored the
///     way the CDN stores an authored file, under <c>{proxy}/{hash}</c>, so once a hash is found here the
///     ordinary CDN download fetches it. One index answers for a whole run, rather than one request per archive.
/// </summary>
public class ProxyIndex
{
    /// <summary>An index that holds nothing. A new one each time, since the type is mutable.</summary>
    public static ProxyIndex Empty => new();

    public int Version { get; set; }

    /// <summary>File sizes, keyed by <see cref="Hash.ToHex" />.</summary>
    public Dictionary<string, long> Files { get; set; } = new();

    /// <summary>Where the files live. Not part of the file: set by whoever loaded the index.</summary>
    [JsonIgnore]
    public Uri? BaseUrl { get; set; }

    /// <summary>
    ///     Whether the proxy holds this exact file. The size is checked too, so an index entry can never stand
    ///     in for a different file that happens to share a hash.
    /// </summary>
    public bool Holds(Hash hash, long size)
    {
        return BaseUrl != null && hash != default && Files.TryGetValue(hash.ToHex(), out var stored) && stored == size;
    }

    public Uri UrlFor(Hash hash)
    {
        if (BaseUrl == null) throw new InvalidOperationException("The proxy index was not loaded from anywhere");
        return new Uri(BaseUrl, hash.ToHex());
    }
}
