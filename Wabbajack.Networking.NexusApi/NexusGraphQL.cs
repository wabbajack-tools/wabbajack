using System;
using System.Collections.Generic;

namespace Wabbajack.Networking.NexusApi;

/// <summary>
///     Every query Wabbajack sends to the Nexus Mods GraphQL API, kept in one place so the tests can check each
///     of them against the schema Nexus publishes. Nexus changes that schema without notice; a query that names a
///     field it no longer has fails the whole request.
/// </summary>
public static class NexusGraphQL
{
    public static readonly Uri Endpoint = new(
        Environment.GetEnvironmentVariable("NEXUS_GRAPHQL_URL") ?? "https://api.nexusmods.com/v2/graphql");

    /// <summary>The summary and description a collection page shows, kept when a new revision is uploaded.</summary>
    public const string CollectionText = @"
        query collectionText($slug: String!) {
          collection(slug: $slug, viewAdultContent: true) {
            summary
            description
          }
        }";

    /// <summary>The signed-in user's collections, newest first; finds the slug of one just created.</summary>
    public const string MyCollections = @"
        query myCollections($count: Int!, $offset: Int!, $viewAdultContent: Boolean!, $viewUnlisted: Boolean!, $viewUnderModeration: Boolean!) {
          myCollections(
            count: $count,
            offset: $offset,
            viewAdultContent: $viewAdultContent,
            viewUnlisted: $viewUnlisted,
            viewUnderModeration: $viewUnderModeration,
            sortBy: ""created_at"",
            sortDirection: ""DESC""
          ) {
            nodes {
              id
              slug
              name
              game { domainName }
              draftRevisionNumber
            }
          }
        }";

    /// <summary>A revision's download link and the game it is for, used to install a collection.</summary>
    public const string CollectionRevisionDownload = @"
        query collectionRevision($slug: String!, $revision: Int) {
          collectionRevision(slug: $slug, revision: $revision) {
            id
            revisionNumber
            collectionId
            downloadLink
            collection {
              id
              slug
              name
              game {
                id
                name
                domainName
              }
            }
          }
        }";

    /// <summary>The latest revision number of a collection, shown on the compiler page before publishing.</summary>
    public const string LatestRevisionNumber = @"
        query collectionRevision($slug: String!, $domainName: String!) {
          collectionRevision(slug: $slug, domainName: $domainName) {
            revisionNumber
          }
        }";

    /// <summary>Every query above, by name, for the contract tests.</summary>
    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>
    {
        [nameof(CollectionText)] = CollectionText,
        [nameof(MyCollections)] = MyCollections,
        [nameof(CollectionRevisionDownload)] = CollectionRevisionDownload,
        [nameof(LatestRevisionNumber)] = LatestRevisionNumber
    };
}
