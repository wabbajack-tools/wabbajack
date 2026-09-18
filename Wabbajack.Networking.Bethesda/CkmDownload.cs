using System.Text.Json;
using System.Text.RegularExpressions;

namespace Wabbajack.Networking.Bethesda;

/// <summary>
///     Finding the <c>.ckm</c> to fetch inside a resolved content record.
///     <para>
///         A record carries more than one download slot for the same Creation, nested at a depth that has
///         changed before, so they are found by walking the record for any <c>download_url</c> rather than
///         by naming a path through it. Two slots that were seen live: the canonical
///         <c>CSV2_&lt;id&gt;.ckm?versionId=...</c>, whose <c>checksum</c> is a bare MD5 of the whole file,
///         and an alternate <c>Mod.ckm</c> whose checksum is a multipart etag and therefore not an MD5 of
///         anything. The presigned one is preferred, and an etag is never reported as a checksum, because a
///         checksum that cannot be verified is worse than none: it invites a comparison that always fails.
///     </para>
/// </summary>
public static partial class CkmDownload
{
    /// <summary>Every <c>.ckm</c> slot in one record, in the order the walk finds them.</summary>
    public static IReadOnlyList<CkmSlot> Slots(JsonElement record)
    {
        var title = TextOf(record, "title");
        var recordId = NumberOf(record, "content_id");

        var slots = new List<CkmSlot>();
        Walk(record, slots, title, recordId);
        return slots;
    }

    /// <summary>
    ///     The slot worth fetching: presigned first, then one whose checksum is a real MD5. Null when the
    ///     record carries no <c>.ckm</c> at all, which is a record that resolved but has nothing to
    ///     download.
    /// </summary>
    public static CkmSlot? Best(JsonElement record)
    {
        return Best(Slots(record));
    }

    /// <summary>One best slot per content id, across every record in a resolve response.</summary>
    public static IReadOnlyList<CkmSlot> BestPerContent(IEnumerable<JsonElement> records)
    {
        return records
            .SelectMany(Slots)
            .GroupBy(s => s.ContentId)
            .Select(g => Best(g.ToArray())!)
            .OrderBy(s => s.ContentId)
            .ToArray();
    }

    private static CkmSlot? Best(IReadOnlyList<CkmSlot> slots)
    {
        return slots
            .OrderByDescending(s => s.HasVersionId)
            .ThenByDescending(s => s.ChecksumIsMd5)
            .FirstOrDefault();
    }

    private static void Walk(JsonElement node, List<CkmSlot> slots, string? title, long recordId)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in node.EnumerateObject())
                {
                    if (property.NameEquals("download_url") && property.Value.ValueKind == JsonValueKind.String)
                    {
                        var slot = SlotFrom(node, property.Value.GetString(), title, recordId);
                        if (slot is not null) slots.Add(slot);
                    }

                    Walk(property.Value, slots, title, recordId);
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in node.EnumerateArray())
                    Walk(item, slots, title, recordId);
                break;
        }
    }

    private static CkmSlot? SlotFrom(JsonElement owner, string? url, string? title, long recordId)
    {
        if (string.IsNullOrWhiteSpace(url) || !url.Contains(".ckm", StringComparison.OrdinalIgnoreCase))
            return null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;

        var match = UrlParts().Match(url);
        var contentId = match.Success && long.TryParse(match.Groups[1].Value, out var fromUrl) ? fromUrl : recordId;

        var checksum = Checksum(TextOf(owner, "checksum")
                                ?? TextOf(owner, "checksum_md5")
                                ?? TextOf(owner, "etag"));

        return new CkmSlot(contentId, title, uri, checksum, NullableNumberOf(owner, "size"));
    }

    private static string? Checksum(string? raw)
    {
        var cleaned = raw?.Trim().Trim('"').ToLowerInvariant();
        return string.IsNullOrEmpty(cleaned) ? null : cleaned;
    }

    private static string? TextOf(JsonElement node, string name)
    {
        return node.ValueKind == JsonValueKind.Object
               && node.TryGetProperty(name, out var value)
               && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static long? NullableNumberOf(JsonElement node, string name)
    {
        if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(name, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(value.GetString(), out var number) => number,
            _ => null
        };
    }

    private static long NumberOf(JsonElement node, string name)
    {
        return NullableNumberOf(node, name) ?? 0;
    }

    /// <summary>
    ///     <c>/public/SKYRIM/client/&lt;content id&gt;/&lt;slot&gt;/&lt;file&gt;.ckm</c>. The id in the path
    ///     is what the URL is really about, so it wins over the record's own when the two disagree.
    /// </summary>
    [GeneratedRegex(@"/client/(\d+)/[^/]+/[^/?]+\.ckm", RegexOptions.IgnoreCase)]
    private static partial Regex UrlParts();
}

/// <summary>One downloadable <c>.ckm</c>.</summary>
/// <param name="ContentId">The Creation it belongs to.</param>
/// <param name="Title">What Bethesda calls it, when the record said. For messages.</param>
/// <param name="Url">Presigned; fetched with <c>user-agent: bnet</c> and nothing else.</param>
/// <param name="Checksum">Whatever the record called a checksum, lowercased and unquoted. May be an etag.</param>
/// <param name="Size">The bytes the record expects, when it said.</param>
public sealed record CkmSlot(long ContentId, string? Title, Uri Url, string? Checksum, long? Size)
{
    /// <summary>Whether the URL carries the presigned <c>versionId</c>, which is part of the object's path.</summary>
    public bool HasVersionId => Url.Query.Contains("versionId=", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Whether <see cref="Checksum" /> is a bare MD5 and so something the downloaded bytes can actually
    ///     be checked against. A multipart etag looks like a hash and is not one.
    /// </summary>
    public bool ChecksumIsMd5 => Checksum is {Length: 32} && Checksum.All(Uri.IsHexDigit);
}
