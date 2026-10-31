using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wabbajack.Paths;

namespace Wabbajack.Networking.Bethesda;

/// <summary>
///     The committed table of Anniversary Edition Creations, and the one question asked of it: which
///     Creation does a wanted game file come out of.
///     <para>
///         A Creation unpacks to two files, a plugin and an archive, and they share a stem -
///         <c>ccBGSSSE002-ExoticArrows.esl</c> beside <c>ccBGSSSE002-ExoticArrows.bsa</c>. The table only
///         records the plugin, so matching is on the stem and never on the whole name, ordinal and
///         case-insensitive because the table itself is inconsistent about case (<c>ccBGSSSE041</c> in one
///         row, <c>ccbgssse041</c> in the next) and the file on disk need not agree with either.
///     </para>
///     <para>
///         Skyrim Special Edition only. The table, the ids and the whole chain were confirmed against that
///         game and nothing else, so a second game is a second table rather than something to assume.
///     </para>
/// </summary>
public sealed class CreationIndex
{
    /// <summary>
    ///     How many rows the table has to have. A short table means it was truncated or regenerated badly,
    ///     and the failure that causes otherwise is a Creation quietly reported as unknown much later.
    /// </summary>
    public const int ExpectedCount = 74;

    private const string ResourceName = "Wabbajack.Networking.Bethesda.Data.creations.json";

    private static readonly Lazy<CreationIndex> Embedded = new(LoadEmbedded, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly IReadOnlyDictionary<string, Creation> _byStem;

    private CreationIndex(IReadOnlyList<Creation> creations)
    {
        Creations = creations;
        ContentIds = creations.Select(c => c.ContentId).ToArray();

        var byStem = new Dictionary<string, Creation>(StringComparer.OrdinalIgnoreCase);
        foreach (var creation in creations)
        {
            var stem = StemOf(creation.Plugin);
            if (!byStem.TryAdd(stem, creation))
                throw new InvalidDataException(
                    $"Two Creations share the plugin stem '{stem}', so neither can be resolved from a file name.");
        }

        _byStem = byStem;
    }

    /// <summary>The table as shipped. Parsed once.</summary>
    public static CreationIndex Default => Embedded.Value;

    /// <summary>Every Creation, in the order the game's own table lists them.</summary>
    public IReadOnlyList<Creation> Creations { get; }

    /// <summary>Every content id, for the one resolve call that asks about all of them at once.</summary>
    public IReadOnlyList<long> ContentIds { get; }

    /// <summary>Parses a table in the shipped shape. Public so a test can hand it one.</summary>
    public static CreationIndex Load(string json)
    {
        var table = JsonSerializer.Deserialize<CreationTable>(json, JsonOptions)
                    ?? throw new InvalidDataException("The Creations table is empty.");
        if (table.Creations is null || table.Creations.Count == 0)
            throw new InvalidDataException("The Creations table lists no Creations.");

        return new CreationIndex(table.Creations);
    }

    private static CreationIndex LoadEmbedded()
    {
        var assembly = typeof(CreationIndex).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException(
                               $"Embedded resource '{ResourceName}' is missing from {assembly.GetName().Name}.");
        using var reader = new StreamReader(stream);
        var index = Load(reader.ReadToEnd());
        if (index.Creations.Count != ExpectedCount)
            throw new InvalidDataException(
                $"The Creations table has {index.Creations.Count} rows, expected {ExpectedCount}; it is truncated or stale.");
        return index;
    }

    /// <summary>
    ///     The Creation a game file belongs to, or null when it is not Creation Club content at all - which
    ///     is the ordinary answer for nearly every file a modlist asks about.
    /// </summary>
    public Creation? Find(RelativePath gameFile)
    {
        // Parts rather than FileName: a default path has none at all and an empty one has an empty array,
        // and FileName indexes into it either way.
        return gameFile.Parts is null or {Length: 0} ? null : Find(gameFile.Parts[^1]);
    }

    /// <inheritdoc cref="Find(RelativePath)" />
    public Creation? Find(string fileName)
    {
        var stem = StemOf(fileName);
        return stem.Length == 0 ? null : _byStem.GetValueOrDefault(stem);
    }

    /// <summary>The content id a game file comes out of, if any.</summary>
    public bool TryGetContentId(RelativePath gameFile, out long contentId)
    {
        var creation = Find(gameFile);
        contentId = creation?.ContentId ?? 0;
        return creation is not null;
    }

    /// <inheritdoc cref="TryGetContentId(RelativePath,out long)" />
    public bool TryGetContentId(string fileName, out long contentId)
    {
        var creation = Find(fileName);
        contentId = creation?.ContentId ?? 0;
        return creation is not null;
    }

    /// <summary>
    ///     The part of a file name that a Creation's two files have in common: the leaf, with its extension
    ///     removed. Both separators are honoured because a name may come from the table, from a
    ///     <see cref="RelativePath" />, or out of a container header written with backslashes.
    /// </summary>
    public static string StemOf(string fileName)
    {
        var leaf = LeafOf(fileName);
        var dot = leaf.LastIndexOf('.');
        return dot > 0 ? leaf[..dot] : leaf;
    }

    private static string LeafOf(string fileName)
    {
        var cut = fileName.AsSpan().LastIndexOfAny('\\', '/');
        return cut < 0 ? fileName : fileName[(cut + 1)..];
    }

    private sealed record CreationTable
    {
        [JsonPropertyName("creations")] public List<Creation>? Creations { get; init; }
    }
}
