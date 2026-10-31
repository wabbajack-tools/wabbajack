using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Wabbajack.Compiler.Test.NexusCollections.Contract;
using Wabbajack.Networking.NexusApi;
using Xunit;

namespace Wabbajack.Compiler.Test.NexusCollections;

/// <summary>
///     The offline half of the Nexus contract: every query Wabbajack sends is valid against the GraphQL
///     snapshot, the snapshots cover everything the code calls, and the checkers themselves catch what they are
///     there to catch. <see cref="NexusContractLiveTests" /> holds the snapshots to what Nexus serves.
/// </summary>
public class NexusContractTests
{
    /// <summary>
    ///     Fields Nexus has marked deprecated that the code still uses. A new deprecation fails
    ///     <see cref="NoQueryUsesAFieldNexusHasNewlyDeprecated" /> so it is seen while there is time to move.
    /// </summary>
    private static readonly HashSet<string> KnownDeprecations = new()
    {
        // "Deprecated- Use collectionsV2." Only the uploader's fallback slug lookup uses it.
        "Query.myCollections"
    };

    public static IEnumerable<object[]> Queries => NexusGraphQL.All.Keys.Select(k => new object[] {k});

    [Theory]
    [MemberData(nameof(Queries))]
    public void Query_IsValidAgainstTheSnapshot(string name)
    {
        var errors = GraphQLContract.Validate(ContractFiles.Load(GraphQLContract.SnapshotFile), NexusGraphQL.All[name]);

        Assert.True(errors.Count == 0, $"{name}:\n  " + string.Join("\n  ", errors));
    }

    [Fact]
    public void NoQueryUsesAFieldNexusHasNewlyDeprecated()
    {
        var deprecated = ((JsonObject) ContractFiles.Load(GraphQLContract.SnapshotFile)["types"]!)
            .SelectMany(type => ((JsonObject) type.Value!)
                .Where(field => field.Value!["deprecated"]!.GetValue<bool>())
                .Select(field => $"{type.Key}.{field.Key}: {field.Value!["deprecationReason"]}"))
            .Where(d => !KnownDeprecations.Contains(d[..d.IndexOf(':')]))
            .ToList();

        Assert.True(deprecated.Count == 0, "Nexus has deprecated fields these queries use:\n  " + string.Join("\n  ", deprecated));
    }

    [Fact]
    public void OpenApiSnapshot_CoversEveryOperationTheUploaderCalls()
    {
        var operations = (JsonObject) ContractFiles.Load(OpenApiContract.SnapshotFile)["operations"]!;

        Assert.Equal(OpenApiContract.Operations.OrderBy(o => o), operations.Select(o => o.Key).OrderBy(o => o));
    }

    [Theory]
    [InlineData("query { collection(slug: \"x\") { summery } }", "Collection.summery does not exist")]
    [InlineData("query { collection(slug: \"x\", adult: true) { slug } }", "has no argument adult")]
    [InlineData("query q($r: String) { collectionRevision(slug: \"x\", revision: $r) { id } }", "cannot be passed")]
    [InlineData("query { collectionRevision(slug: 5) { id } }", "cannot take IntValue")]
    public void GraphQLChecker_RejectsWhatNexusWould(string query, string expected)
    {
        var errors = GraphQLContract.Validate(ContractFiles.Load(GraphQLContract.SnapshotFile), query);

        Assert.Contains(errors, e => e.Contains(expected));
    }

    [Fact]
    public void OpenApiChecker_RejectsABodyOutsideTheSchema()
    {
        var validator = new OpenApiValidator(ContractFiles.Load(OpenApiContract.SnapshotFile));
        var body = new JsonObject
        {
            ["upload_id"] = "not-a-uuid",
            ["collection_data"] = new JsonObject
            {
                ["adult_content"] = false,
                ["collection_schema_id"] = 2,
                ["collection_manifest"] = new JsonObject
                {
                    ["info"] = new JsonObject {["author"] = "a", ["name"] = "xy", ["domain_name"] = "skyrimspecialedition"},
                    ["mods"] = new JsonArray(new JsonObject
                    {
                        ["name"] = "m", ["version"] = "1", ["optional"] = false, ["domain_name"] = "skyrimspecialedition",
                        ["source"] = new JsonObject {["type"] = "torrent", ["mod_id"] = 5}
                    })
                }
            },
            ["extra"] = true
        };

        var errors = validator.ValidateSchemaRef(body, "CreateCollectionRequest");

        Assert.Contains("/upload_id: not a uuid", errors);
        Assert.Contains("/collection_data/collection_manifest/info/name: shorter than 3", errors);
        Assert.Contains(errors, e => e.StartsWith("/collection_data/collection_manifest/mods/0/source/type:"));
        Assert.Contains("/collection_data/collection_manifest/mods/0/source/mod_id: expected a string", errors);
        Assert.Contains("/extra: not in the schema", errors);
    }
}
