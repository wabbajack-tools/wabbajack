using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wabbajack.Networking.Steam.DTOs;

public class AppInfo
{
    /// <summary>
    ///     Steam's <c>depots</c> section verbatim.
    ///     It is not a dictionary of depots, whatever the name suggests: alongside the numbered depot
    ///     entries it carries <c>branches</c>, <c>baselanguages</c>, <c>hasdepotsindlc</c> and others, some
    ///     of which are bare strings. Typing the whole section as depots makes reading any real app's info
    ///     throw, so the raw values are kept and <see cref="Depots" /> picks out the ones that are depots.
    /// </summary>
    [JsonPropertyName("depots")]
    public Dictionary<string, JsonElement> DepotSection { get; set; } = new();

    /// <summary>
    ///     The numbered depot entries, keyed by depot id. Anything in the section that is not a numbered
    ///     object is skipped rather than guessed at.
    ///     Takes the options rather than using the defaults because Steam writes every scalar as a string,
    ///     so reading one back as a number needs <c>AllowReadingFromString</c>, which is what
    ///     <c>DTOSerializer</c> is configured with.
    /// </summary>
    public IEnumerable<(uint DepotId, Depot Depot)> GetDepots(JsonSerializerOptions options)
    {
        foreach (var (key, value) in DepotSection)
        {
            if (!uint.TryParse(key, out var depotId)) continue;
            if (value.ValueKind != JsonValueKind.Object) continue;

            var depot = value.Deserialize<Depot>(options);
            if (depot != null) yield return (depotId, depot);
        }
    }
}

public class Depot
{
    [JsonPropertyName("name")] public string Name { get; set; }

    [JsonPropertyName("config")] public DepotConfig Config { get; set; }

    [JsonPropertyName("maxsize")] public ulong MaxSize { get; set; }

    [JsonPropertyName("depotfromapp")] public uint DepotFromApp { get; set; }

    [JsonPropertyName("sharedinstall")] public uint SharedInstall { get; set; }

    /// <summary>
    ///     Branch name to manifest, raw. Steam has written this two ways over the years: a bare manifest id
    ///     as a string, and an object whose <c>gid</c> is that id alongside the download sizes. Both are
    ///     still in the wild, so neither is assumed.
    /// </summary>
    [JsonPropertyName("manifests")]
    public Dictionary<string, JsonElement> Manifests { get; set; } = new();

    /// <summary>The manifest id currently published on a branch, or null when the branch has none.</summary>
    public ulong? ManifestFor(string branch)
    {
        if (!Manifests.TryGetValue(branch, out var value)) return null;

        var raw = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Object => value.TryGetProperty("gid", out var gid) ? gid.GetString() : null,
            _ => null
        };

        return ulong.TryParse(raw, out var manifestId) ? manifestId : null;
    }
}

public class DepotConfig
{
    [JsonPropertyName("oslist")] public string OSList { get; set; }

    [JsonPropertyName("language")] public string Language { get; set; }
}
