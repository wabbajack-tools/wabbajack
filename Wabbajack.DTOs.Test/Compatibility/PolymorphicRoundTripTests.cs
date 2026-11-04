using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Wabbajack.DTOs.JsonConverters;
using Xunit;

namespace Wabbajack.DTOs.Test.Compatibility;

/// <summary>
///     Every download state, directive and BSA archive or file state is written to modlist JSON by a
///     converter in the checked-in Generated.cs, which only changes when someone reruns
///     Wabbajack.DTOs.ConverterGenerators. A type or property added without rerunning it is silently
///     dropped from every list compiled afterwards, and the installer finds out much later. These tests
///     find the types by reflection, fill every property, and check each one survives the trip.
/// </summary>
public class PolymorphicRoundTripTests
{
    private readonly DTOSerializer _serializer;

    public PolymorphicRoundTripTests(DTOSerializer serializer)
    {
        _serializer = serializer;
    }

    public static IEnumerable<object[]> ConcreteTypes =>
        ModlistTypes.AllConcreteTypes().Select(t => new object[] {t.Family, t.Type});

    /// <summary>
    ///     The generator throws for a type with no JsonName, so a type without one can only exist if the
    ///     generator has not been run since it was added.
    /// </summary>
    [Theory]
    [MemberData(nameof(ConcreteTypes))]
    public void EveryConcreteTypeHasAJsonName(Type family, Type type)
    {
        Assert.False(string.IsNullOrEmpty(ModlistTypes.JsonName(type)),
            $"{type.FullName} implements {family.Name} but has no [JsonName]");
    }

    /// <summary>
    ///     Written through the base type, as it is inside an Archive or a ModList, every serialized
    ///     property comes back with the value it went in with.
    /// </summary>
    [Theory]
    [MemberData(nameof(ConcreteTypes))]
    public void EveryPropertyRoundTripsThroughTheBaseType(Type family, Type type)
    {
        var original = new SampleBuilder().Build(type);

        var json = JsonSerializer.Serialize(original, family, _serializer.Options);
        var read = JsonSerializer.Deserialize(json, family, _serializer.Options);

        Assert.NotNull(read);
        Assert.IsType(type, read);
        DeepCompare.AssertEqual(original, read, type.Name);
        Assert.Equal(json, JsonSerializer.Serialize(read, family, _serializer.Options));
    }

    /// <summary>
    ///     The base type's converter hands writing to the concrete one, so the two have to agree on the
    ///     output. A switch case missing from the base converter would otherwise write nothing.
    /// </summary>
    [Theory]
    [MemberData(nameof(ConcreteTypes))]
    public void BaseAndConcreteConvertersWriteTheSameJson(Type family, Type type)
    {
        var original = new SampleBuilder().Build(type);

        var viaBase = JsonSerializer.Serialize(original, family, _serializer.Options);
        var viaConcrete = JsonSerializer.Serialize(original, type, _serializer.Options);

        Assert.Equal(viaConcrete, viaBase);
    }

    /// <summary>
    ///     The reader insists on <c>$type</c> being the first property, so the writer has to put it there,
    ///     and it has to be the type's JsonName rather than one of its aliases.
    /// </summary>
    [Theory]
    [MemberData(nameof(ConcreteTypes))]
    public void TypeIsWrittenFirstAsTheJsonName(Type family, Type type)
    {
        var json = JsonSerializer.Serialize(new SampleBuilder().Build(type), family, _serializer.Options);

        using var doc = JsonDocument.Parse(json);
        var first = doc.RootElement.EnumerateObject().First();
        Assert.Equal("$type", first.Name);
        Assert.Equal(ModlistTypes.JsonName(type), first.Value.GetString());
    }

    /// <summary>
    ///     The <c>$type</c> each type writes today. Every Wabbajack release in users' hands reads only
    ///     the names it knew about, so changing one of these makes every list compiled afterwards
    ///     unreadable by those releases even though this build reads it fine. Add the alias instead,
    ///     and only change a row here on purpose.
    /// </summary>
    public static readonly Dictionary<string, string> WrittenNames = new()
    {
        ["Bethesda"] = "Bethesda",
        ["DeprecatedLoversLab"] = "LoversLabDownloader, Wabbajack.Lib",
        ["GameFileSource"] = "GameFileSourceDownloader, Wabbajack.Lib",
        ["GoogleDrive"] = "GoogleDriveDownloader, Wabbajack.Lib",
        ["Http"] = "HttpDownloader, Wabbajack.Lib",
        ["LoversLab"] = "LoversLabOAuthDownloader, Wabbajack.Lib",
        ["Manual"] = "ManualDownloader, Wabbajack.Lib",
        ["MediaFire"] = "MediaFireDownloader+State, Wabbajack.Lib",
        ["Mega"] = "MegaDownloader, Wabbajack.Lib",
        ["ModDB"] = "ModDBDownloader, Wabbajack.Lib",
        ["Nexus"] = "NexusDownloader, Wabbajack.Lib",
        ["TESAlliance"] = "TESAllianceDownloader, Wabbajack.Lib",
        ["VectorPlexus"] = "VectorPlexusOAuthDownloader+State, Wabbajack.Lib",
        ["WabbajackCDN"] = "WabbajackCDNDownloader+State, Wabbajack.Lib",

        ["ArchiveMeta"] = "ArchiveMeta",
        ["CreateBSA"] = "CreateBSA",
        ["FromArchive"] = "FromArchive",
        ["IgnoredDirectly"] = "IgnoredDirectly",
        ["InlineFile"] = "InlineFile",
        ["MergedPatch"] = "MergedPatch, Wabbajack.Lib",
        ["NoMatch"] = "NoMatch",
        ["PatchedFromArchive"] = "PatchedFromArchive",
        ["PropertyFile"] = "PropertyFile",
        ["RemappedInlineFile"] = "RemappedInlineFile",
        ["TransformedTexture"] = "TransformedTexture",

        ["BA2State"] = "BA2State, Compression.BSA",
        ["BSAState"] = "BSAState, Compression.BSA",
        ["TES3State"] = "TES3State",

        ["BA2DX10File"] = "BA2DX10Entry",
        ["BA2File"] = "BA2File",
        ["BSAFile"] = "BSAFileState, Compression.BSA",
        ["TES3File"] = "TES3File"
    };

    [Theory]
    [MemberData(nameof(ConcreteTypes))]
    public void WrittenTypeNameIsUnchanged(Type family, Type type)
    {
        Assert.True(WrittenNames.ContainsKey(type.Name),
            $"{type.FullName} ({family.Name}) is new; add the $type it writes to WrittenNames");

        var json = JsonSerializer.Serialize(new SampleBuilder().Build(type), family, _serializer.Options);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(WrittenNames[type.Name], doc.RootElement.GetProperty("$type").GetString());
    }

    [Fact]
    public void WrittenNamesListsOnlyTypesThatExist()
    {
        var names = ModlistTypes.AllConcreteTypes().Select(t => t.Type.Name).ToHashSet();
        var stale = WrittenNames.Keys.Where(k => !names.Contains(k)).ToArray();
        Assert.True(stale.Length == 0, $"No such types: {string.Join(", ", stale)}");
    }

    /// <summary>
    ///     The generator keeps one name-to-type table per family and lets a later type overwrite an
    ///     earlier one's entry, so two types claiming the same name would make one of them unreadable
    ///     without any error.
    /// </summary>
    [Fact]
    public void NoTwoTypesInAFamilyShareAName()
    {
        foreach (var family in ModlistTypes.Families)
        {
            var clashes = ModlistTypes.ConcreteTypes(family)
                .SelectMany(t => ModlistTypes.JsonAliases(t).Append(ModlistTypes.JsonName(t))
                    .Select(n => (Name: n, Type: t)))
                .GroupBy(x => x.Name)
                .Where(g => g.Select(x => x.Type).Distinct().Count() > 1)
                .Select(g => $"{g.Key}: {string.Join(", ", g.Select(x => x.Type.Name))}")
                .ToArray();

            Assert.True(clashes.Length == 0, $"{family.Name}: {string.Join("; ", clashes)}");
        }
    }

    /// <summary>
    ///     The generator only looks at properties, so a public field on one of these types is never
    ///     written to a modlist. The two listed here already lose their value that way:
    ///     <c>IgnoredDirectly.Reason</c> is compiler-only and never meant to be written, and
    ///     <c>PropertyFile.Type</c> is read by nothing today. A new field should be a property instead.
    /// </summary>
    [Fact]
    public void NoNewPublicFieldsThatTheConvertersWouldDrop()
    {
        var known = new HashSet<string> {"IgnoredDirectly.Reason", "PropertyFile.Type"};

        var fields = ModlistTypes.AllConcreteTypes()
            .SelectMany(t => t.Type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => $"{f.DeclaringType!.Name}.{f.Name}"))
            .Distinct()
            .Where(f => !known.Contains(f))
            .ToArray();

        Assert.Empty(fields);
    }
}
