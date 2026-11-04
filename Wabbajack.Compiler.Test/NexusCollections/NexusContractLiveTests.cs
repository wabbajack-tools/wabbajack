using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wabbajack.Compiler.Test.NexusCollections.Contract;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.NexusApi;
using Wabbajack.Services.OSIntegrated;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.Compiler.Test.NexusCollections;

/// <summary>
///     Checks the contract snapshots against what Nexus Mods serves today. Nexus changes these APIs without
///     notice; when one of these fails, the message lists what moved. If Wabbajack still works with the change,
///     refresh the snapshots by running these with <c>NEXUS_CONTRACT_UPDATE=1</c> and review the diff, which also
///     updates what <see cref="FakeNexus" /> accepts and answers. If it does not, the offline tests built on the
///     refreshed snapshot will now fail where the code needs to change.
/// </summary>
public class NexusContractLiveTests
{
    /// <summary>
    ///     A GraphQL error that means the query itself was rejected, rather than that it ran and the answer was
    ///     "not found" or "log in first". These are graphql-ruby's static validation codes.
    /// </summary>
    private static readonly HashSet<string> ValidationCodes = new(StringComparer.Ordinal)
    {
        "undefinedField", "argumentNotAccepted", "missingRequiredArguments", "undefinedType",
        "variableMismatch", "argumentLiteralsIncompatible", "variableNotDefined", "variableNotUsed",
        "selectionMismatch", "fieldsWillMerge", "useAndDefineFragment", "parseError", "syntaxError"
    };

    /// <summary>Variables each query is sent with live. None of them change anything on Nexus.</summary>
    private static readonly Dictionary<string, JsonObject> SampleVariables = new()
    {
        [nameof(NexusGraphQL.CollectionText)] = new JsonObject {["slug"] = "wabbajack-contract-check"},
        [nameof(NexusGraphQL.MyCollections)] = new JsonObject
        {
            ["count"] = 1, ["offset"] = 0, ["viewAdultContent"] = true, ["viewUnlisted"] = true,
            ["viewUnderModeration"] = true
        },
        [nameof(NexusGraphQL.CollectionRevisionDownload)] = new JsonObject {["slug"] = "wabbajack-contract-check"},
        [nameof(NexusGraphQL.LatestRevisionNumber)] = new JsonObject
            {["slug"] = "wabbajack-contract-check", ["domainName"] = "skyrimspecialedition"}
    };

    private readonly ITestOutputHelper _output;
    private readonly ITokenProvider<NexusOAuthState> _nexusLogin;

    public NexusContractLiveTests(ITestOutputHelper output, ITokenProvider<NexusOAuthState> nexusLogin)
    {
        _output = output;
        _nexusLogin = nexusLogin;
    }

    /// <summary>A client of its own: the test host's shared one is the offline fake.</summary>
    private static HttpClient Live() => new() {Timeout = TimeSpan.FromSeconds(60)};

    [Fact]
    [Trait("Category", "RequiresNetwork")]
    public async Task OpenApi_LiveDocumentMatchesTheSnapshot()
    {
        using var client = Live();
        var yaml = await client.GetStringAsync(OpenApiContract.LiveDocument);
        var live = OpenApiContract.Extract(OpenApiContract.FromYaml(yaml));

        CompareOrUpdate(OpenApiContract.SnapshotFile, live);
    }

    [Fact]
    [Trait("Category", "RequiresNetwork")]
    public async Task GraphQL_EveryQueryIsAcceptedLive()
    {
        using var client = Live();
        var rejected = new List<string>();

        foreach (var (name, query) in NexusGraphQL.All)
        {
            var body = new JsonObject {["query"] = query, ["variables"] = SampleVariables[name].DeepClone()};
            using var response = await client.PostAsync(GraphQLContract.LiveEndpoint,
                new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"));
            var reply = JsonNode.Parse(await response.Content.ReadAsStringAsync());

            foreach (var error in (reply?["errors"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
            {
                var code = error["extensions"]?["code"]?.GetValue<string>() ?? "";
                var firstPath = (error["path"] as JsonArray)?.FirstOrDefault()?.GetValue<string>();
                _output.WriteLine($"{name}: {code} {error["message"]}");
                if (ValidationCodes.Contains(code) || firstPath == "query")
                    rejected.Add($"{name}: {error["message"]}");
            }
        }

        Assert.True(rejected.Count == 0,
            "Nexus rejects these queries as written:\n  " + string.Join("\n  ", rejected));
    }

    /// <summary>
    ///     Every field the queries use that the public schema shows must match the snapshot. Fields that only a
    ///     signed-in user can see are covered by <see cref="GraphQL_SignedInSchemaMatchesTheSnapshot" />.
    /// </summary>
    [Fact]
    [Trait("Category", "RequiresNetwork")]
    public async Task GraphQL_PublicSchemaMatchesTheSnapshot()
    {
        var introspection = await Introspect(accessToken: null);
        var publicQueries = NexusGraphQL.All.Where(q => q.Key != nameof(NexusGraphQL.MyCollections))
            .ToDictionary(q => q.Key, q => q.Value);
        var live = GraphQLContract.Extract(introspection, publicQueries);
        var snapshot = ContractFiles.Load(GraphQLContract.SnapshotFile);

        var differences = new List<string>();
        foreach (var (type, fields) in (JsonObject) live["types"]!)
        foreach (var (field, def) in (JsonObject) fields!)
            differences.AddRange(ContractFiles.Diff(snapshot["types"]?[type]?[field], def, $"/types/{type}/{field}"));

        Assert.True(differences.Count == 0,
            "The Nexus GraphQL schema has changed under these queries:\n  " + string.Join("\n  ", differences));
    }

    [Fact]
    [Trait("Category", "RequiresOAuth")]
    public async Task GraphQL_SignedInSchemaMatchesTheSnapshot()
    {
        var introspection = await Introspect(await SignedInAccessToken());
        var live = GraphQLContract.Extract(introspection, NexusGraphQL.All);

        CompareOrUpdate(GraphQLContract.SnapshotFile, live);
    }

    /// <summary>
    ///     The stored Nexus Mods login's access token, refreshed first if it has expired. The refresh goes through
    ///     a <see cref="NexusApi" /> with a real client, the way the app does it, so the new token is stored and the
    ///     login keeps working afterwards; this test host's own client is the offline fake.
    /// </summary>
    private async Task<string> SignedInAccessToken()
    {
        var login = await _nexusLogin.Get();
        if (login?.OAuth is {IsExpired: true})
        {
            var services = new ServiceCollection().AddLogging();
            services.AddOSIntegrated();
            await using var provider = services.BuildServiceProvider();
            await provider.GetRequiredService<NexusApi>().CredentialSource();
            login = await provider.GetRequiredService<ITokenProvider<NexusOAuthState>>().Get();
        }

        Assert.False(login?.OAuth == null || login.OAuth.IsExpired || string.IsNullOrWhiteSpace(login.OAuth.AccessToken),
            "Needs a Nexus Mods login: open Wabbajack and log in to Nexus Mods, then run this again.");
        return login!.OAuth!.AccessToken!;
    }

    private static async Task<JsonNode> Introspect(string? accessToken)
    {
        using var client = Live();
        using var request = new HttpRequestMessage(HttpMethod.Post, GraphQLContract.LiveEndpoint)
        {
            Content = new StringContent(new JsonObject {["query"] = GraphQLContract.IntrospectionQuery}.ToJsonString(),
                Encoding.UTF8, "application/json")
        };
        if (accessToken != null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    }

    private void CompareOrUpdate(string file, JsonObject live)
    {
        if (ContractFiles.Updating)
        {
            var path = ContractFiles.WriteToSource(file, live);
            _output.WriteLine($"Wrote {path}");
            return;
        }

        var differences = ContractFiles.Diff(ContractFiles.Load(file), live);
        Assert.True(differences.Count == 0,
            $"Nexus has changed what {file} records. Review these, then refresh with " +
            $"{ContractFiles.UpdateVariable}=1:\n  " + string.Join("\n  ", differences));
    }
}
