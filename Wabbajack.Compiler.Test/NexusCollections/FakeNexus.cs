using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Wabbajack.Compiler.Test.NexusCollections.Contract;
using Wabbajack.DTOs.Logins;
using Wabbajack.DTOs.OAuth;
using Wabbajack.Networking.Http.Interfaces;

namespace Wabbajack.Compiler.Test.NexusCollections;

/// <summary>
///     An in-process stand-in for the parts of Nexus Mods that publishing and installing a collection use, built
///     from the contract snapshots rather than from what Wabbajack happens to send. Every request is checked
///     against them (a v3 body against its OpenAPI schema, a GraphQL query against the schema subset) and every
///     answer is checked against the response schema before it goes back, so the fake cannot drift from the
///     contract either. Anything that breaks the contract lands in <see cref="Violations" />, which every test
///     asserts is empty.
///     <para>
///         The archive's parts go to presigned "storage" URLs on a host of their own, the way S3 multipart works,
///         and are reassembled and checked against the ETags the complete call lists.
///     </para>
/// </summary>
public class FakeNexus : HttpMessageHandler
{
    public const string AccessToken = "fake-nexus-access-token";
    public const string Domain = "skyrimspecialedition";

    private const string RestBase = "https://api.nexusmods.com/v3";
    private const string StorageHost = "storage.fake-nexus.invalid";

    private static readonly Lazy<JsonObject> OpenApi = new(() => ContractFiles.Load(OpenApiContract.SnapshotFile));
    private static readonly Lazy<JsonObject> GraphQL = new(() => ContractFiles.Load(GraphQLContract.SnapshotFile));

    private readonly OpenApiValidator _validator = new(OpenApi.Value);
    private int _nextId = 1000;

    public ConcurrentQueue<string> Violations { get; } = new();
    public ConcurrentQueue<string> Requests { get; } = new();

    /// <summary>Size of each multipart part the fake hands out. Small, so a test archive spans several.</summary>
    public long PartSizeBytes { get; set; } = 64 * 1024;

    /// <summary>How many polls of an upload answer "created" before it becomes "available".</summary>
    public int PollsBeforeAvailable { get; set; } = 1;

    /// <summary>
    ///     Validation Nexus applies to a collection beyond the schema - a mod that does not exist, a summary it
    ///     will not take - given the request's <c>collection_data</c>; returns the <c>errors</c> of a 422, or null.
    /// </summary>
    public Func<JsonObject, JsonArray?> RejectCollection { get; set; } = _ => null;

    public Dictionary<Guid, FakeUpload> Uploads { get; } = new();
    public List<FakeCollection> Collections { get; } = new();

    /// <summary>The bytes the download link of a collection revision serves.</summary>
    public Dictionary<(string Slug, int Revision), byte[]> RevisionArchives { get; } = new();

    public HttpClient Client() => new(this, false);

    public static ITokenProvider<NexusOAuthState> Login(string accessToken = AccessToken, bool expired = false)
    {
        return new FixedLogin(new NexusOAuthState
        {
            OAuth = new JwtTokenReply
            {
                AccessToken = accessToken,
                ExpiresIn = 3600,
                ReceivedAt = (expired ? DateTime.UtcNow.AddDays(-1) : DateTime.UtcNow).ToFileTimeUtc()
            }
        });
    }

    public FakeCollection AddCollection(string name, int revisions = 1, string summary = "A summary",
        string description = "A description")
    {
        var collection = new FakeCollection
        {
            Id = (++_nextId).ToString(),
            Slug = "fake" + _nextId,
            Name = name,
            DomainName = Domain,
            Summary = summary,
            Description = description
        };
        for (var i = 0; i < revisions; i++)
            collection.Revisions.Add(new FakeRevision {Id = (++_nextId).ToString(), Number = i + 1});
        lock (Collections) Collections.Add(collection);
        return collection;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var url = request.RequestUri!;
        var body = request.Content == null ? null : await request.Content.ReadAsByteArrayAsync(token);
        Requests.Enqueue($"{request.Method} {url}");

        if (url.Host == StorageHost)
            return Storage(request, url, body);

        if (url.Host != "api.nexusmods.com")
            return Violation(HttpStatusCode.NotFound, $"{request.Method} {url}: not a Nexus Mods endpoint the contract covers");

        if (request.Headers.Authorization is not {Scheme: "Bearer"} auth || auth.Parameter != AccessToken)
            return Json(HttpStatusCode.Unauthorized, new JsonObject {["message"] = "Unauthorized"});

        if (url.AbsolutePath == "/v2/graphql" && request.Method == HttpMethod.Post)
            return GraphQLRequest(body);

        if (url.AbsolutePath.StartsWith("/v1/games/") && url.AbsolutePath.EndsWith("/download_link.json"))
            return DownloadLink(url);

        if (url.ToString().StartsWith(RestBase + "/"))
            return Rest(request, url.AbsolutePath["/v3".Length..], body);

        return Violation(HttpStatusCode.NotFound, $"{request.Method} {url}: not a Nexus Mods endpoint the contract covers");
    }

    // ---- v3 REST ----

    private HttpResponseMessage Rest(HttpRequestMessage request, string path, byte[]? body)
    {
        var (operation, args) = MatchOperation(request.Method.Method, path);
        if (operation == null)
            return Violation(HttpStatusCode.NotFound, $"{request.Method} {path}: not in the v3 contract");

        var definition = OpenApi.Value["operations"]![operation]!;
        JsonObject? json = null;
        if (definition["requestBody"]?["content"]?["application/json"]?["schema"] is { } schema)
        {
            if (request.Content?.Headers.ContentType?.MediaType != "application/json")
                Violations.Enqueue($"{operation}: body is not sent as application/json");
            json = body == null ? null : JsonNode.Parse(body) as JsonObject;
            var errors = _validator.Validate(json, schema);
            if (errors.Count > 0)
            {
                foreach (var e in errors) Violations.Enqueue($"{operation} request {e}");
                return Problem(HttpStatusCode.UnprocessableEntity, "Request does not match the schema");
            }
        }

        var (status, reply) = operation switch
        {
            "POST /uploads/multipart" => CreateMultipart(json!),
            "GET /uploads/{id}" => GetUpload(args["id"]),
            "POST /uploads/{id}/finalise" => Finalise(args["id"]),
            "POST /collections" => CreateCollection(json!, null),
            "POST /collections/{id}/revisions" => CreateCollection(json!, args["id"]),
            _ => throw new InvalidOperationException(operation)
        };

        CheckResponse(operation, status, reply);
        return Json(status, reply, (int) status >= 400 ? "application/problem+json" : "application/json");
    }

    private static (string? Operation, Dictionary<string, string> Args) MatchOperation(string method, string path)
    {
        foreach (var operation in OpenApiContract.Operations)
        {
            var (opMethod, template) = OpenApiContract.Split(operation);
            if (!opMethod.Equals(method, StringComparison.OrdinalIgnoreCase)) continue;
            var pattern = "^" + Regex.Replace(Regex.Escape(template), @"\\\{(\w+)}", "(?<$1>[^/]+)") + "$";
            var match = Regex.Match(path, pattern);
            if (!match.Success) continue;
            return (operation, match.Groups.Keys.Where(k => !int.TryParse(k, out _))
                .ToDictionary(k => k, k => Uri.UnescapeDataString(match.Groups[k].Value)));
        }

        return (null, new Dictionary<string, string>());
    }

    /// <summary>The fake's own answers have to satisfy the contract too, or a test could pass against a lie.</summary>
    private void CheckResponse(string operation, HttpStatusCode status, JsonNode reply)
    {
        var responses = OpenApi.Value["operations"]![operation]!["responses"]!;
        var declared = responses[((int) status).ToString()];
        if (declared == null)
        {
            Violations.Enqueue($"{operation}: the fake answered {(int) status}, which the contract does not list");
            return;
        }

        var content = declared["content"] as JsonObject;
        var schema = content?.FirstOrDefault().Value?["schema"];
        if (schema == null) return;
        foreach (var e in _validator.Validate(reply, schema))
            Violations.Enqueue($"{operation}: the fake's own {(int) status} answer breaks the contract at {e}");
    }

    private (HttpStatusCode, JsonNode) CreateMultipart(JsonObject json)
    {
        var size = json["size_bytes"]!.GetValue<long>();
        var upload = new FakeUpload
        {
            Id = Guid.NewGuid(),
            SizeBytes = size,
            Filename = json["filename"]!.GetValue<string>(),
            PartSize = PartSizeBytes
        };
        lock (Uploads) Uploads[upload.Id] = upload;

        var parts = (int) Math.Max(1, (size + PartSizeBytes - 1) / PartSizeBytes);
        var data = UploadJson(upload);
        data["part_size_bytes"] = PartSizeBytes;
        data["part_presigned_urls"] = new JsonArray(Enumerable.Range(1, parts)
            .Select(n => (JsonNode) $"https://{StorageHost}/{upload.Id}/part/{n}?signature=fake").ToArray());
        data["complete_presigned_url"] = $"https://{StorageHost}/{upload.Id}/complete?signature=fake";
        return (HttpStatusCode.Created, new JsonObject {["data"] = data});
    }

    private (HttpStatusCode, JsonNode) GetUpload(string id)
    {
        if (!Guid.TryParse(id, out var guid) || !Uploads.TryGetValue(guid, out var upload))
            return (HttpStatusCode.NotFound, ProblemBody(HttpStatusCode.NotFound, "Upload not found"));
        if (upload.Finalised && upload.State == "created" && ++upload.Polls > PollsBeforeAvailable)
            upload.State = "available";
        return (HttpStatusCode.OK, new JsonObject {["data"] = UploadJson(upload)});
    }

    private (HttpStatusCode, JsonNode) Finalise(string id)
    {
        if (!Guid.TryParse(id, out var guid) || !Uploads.TryGetValue(guid, out var upload))
            return (HttpStatusCode.NotFound, ProblemBody(HttpStatusCode.NotFound, "Upload not found"));
        if (upload.Assembled == null)
        {
            Violations.Enqueue($"upload {id} was finalised before the multipart upload was completed");
            return (HttpStatusCode.UnprocessableEntity, ProblemBody(HttpStatusCode.UnprocessableEntity, "Upload is not complete"));
        }

        upload.Finalised = true;
        return (HttpStatusCode.OK, new JsonObject {["data"] = UploadJson(upload)});
    }

    private static JsonObject UploadJson(FakeUpload upload)
    {
        return new JsonObject
        {
            ["id"] = upload.Id.ToString(),
            ["user"] = new JsonObject {["id"] = "4242"},
            ["state"] = upload.State
        };
    }

    private (HttpStatusCode, JsonNode) CreateCollection(JsonObject json, string? collectionId)
    {
        var uploadId = Guid.Parse(json["upload_id"]!.GetValue<string>());
        if (!Uploads.TryGetValue(uploadId, out var upload) || upload.State != "available")
            return (HttpStatusCode.UnprocessableEntity,
                ValidationBody(new JsonArray(new JsonObject {["detail"] = "Upload is not available", ["pointer"] = "/upload_id"})));
        if (upload.Claimed)
            return (HttpStatusCode.UnprocessableEntity,
                ValidationBody(new JsonArray(new JsonObject {["detail"] = "Upload has already been claimed", ["pointer"] = "/upload_id"})));

        var data = (JsonObject) json["collection_data"]!;
        if (RejectCollection(data) is { Count: > 0 } errors)
            return (HttpStatusCode.UnprocessableEntity, ValidationBody(errors));

        FakeCollection collection;
        if (collectionId == null)
        {
            var info = data["collection_manifest"]!["info"]!;
            collection = AddCollection(info["name"]!.GetValue<string>(), revisions: 0,
                info["summary"]?.GetValue<string>() ?? "", info["description"]?.GetValue<string>() ?? "");
            collection.DomainName = info["domain_name"]!.GetValue<string>();
        }
        else
        {
            collection = Collections.FirstOrDefault(c => c.Id == collectionId)!;
            if (collection == null)
                return (HttpStatusCode.NotFound, ProblemBody(HttpStatusCode.NotFound, "Collection not found"));
        }

        upload.Claimed = true;
        var revision = new FakeRevision
        {
            Id = (++_nextId).ToString(),
            Number = collection.Revisions.Count + 1,
            Upload = upload,
            Payload = (JsonObject) data.DeepClone()
        };
        collection.Revisions.Add(revision);
        RevisionArchives[(collection.Slug, revision.Number)] = upload.Assembled!;

        var reply = collectionId == null
            ? new JsonObject
            {
                ["id"] = collection.Id, ["slug"] = collection.Slug, ["revision_id"] = revision.Id,
                ["revision_number"] = revision.Number, ["revision_status"] = "draft"
            }
            : new JsonObject
            {
                ["id"] = revision.Id, ["collection_id"] = collection.Id,
                ["revision_number"] = revision.Number, ["revision_status"] = "draft"
            };
        return (HttpStatusCode.OK, new JsonObject {["data"] = reply});
    }

    // ---- presigned storage ----

    private HttpResponseMessage Storage(HttpRequestMessage request, Uri url, byte[]? body)
    {
        if (request.Headers.Authorization != null)
            Violations.Enqueue($"{request.Method} {url}: presigned storage was sent an Authorization header");

        var segments = url.AbsolutePath.Trim('/').Split('/');
        if (request.Method == HttpMethod.Get && segments is ["collections", var slug, var file] &&
            int.TryParse(file.Replace(".7z", ""), out var rev) && RevisionArchives.TryGetValue((slug, rev), out var archive))
            return new HttpResponseMessage(HttpStatusCode.OK) {Content = new ByteArrayContent(archive)};

        if (!Guid.TryParse(segments[0], out var id) || !Uploads.TryGetValue(id, out var upload))
            return new HttpResponseMessage(HttpStatusCode.Forbidden);

        if (request.Method == HttpMethod.Put && segments is [_, "part", var n])
        {
            var part = int.Parse(n);
            upload.Parts[part] = body ?? Array.Empty<byte>();
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Headers.ETag = new EntityTagHeaderValue("\"" + Convert.ToHexString(MD5.HashData(upload.Parts[part])).ToLowerInvariant() + "\"");
            return response;
        }

        if (request.Method == HttpMethod.Post && segments is [_, "complete"])
            return CompleteMultipart(upload, body);

        return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
    }

    /// <summary>S3's CompleteMultipartUpload: every part listed in order with the ETag its upload returned.</summary>
    private HttpResponseMessage CompleteMultipart(FakeUpload upload, byte[]? body)
    {
        var listed = XDocument.Parse(Encoding.UTF8.GetString(body ?? Array.Empty<byte>())).Root!.Elements("Part")
            .Select(p => (Number: int.Parse(p.Element("PartNumber")!.Value), ETag: p.Element("ETag")!.Value.Trim('"')))
            .ToList();

        var expected = Enumerable.Range(1, upload.Parts.Count).ToList();
        if (!listed.Select(p => p.Number).SequenceEqual(expected))
            return StorageError(upload, $"parts listed as [{string.Join(",", listed.Select(p => p.Number))}], uploaded {upload.Parts.Count}");

        foreach (var (number, etag) in listed)
        {
            var actual = Convert.ToHexString(MD5.HashData(upload.Parts[number])).ToLowerInvariant();
            if (!etag.Equals(actual, StringComparison.OrdinalIgnoreCase))
                return StorageError(upload, $"part {number} listed with ETag {etag}, uploaded as {actual}");
            if (number < listed.Count && upload.Parts[number].Length != upload.PartSize)
                return StorageError(upload, $"part {number} is {upload.Parts[number].Length} bytes, not the {upload.PartSize} the session set");
        }

        upload.Assembled = listed.SelectMany(p => upload.Parts[p.Number]).ToArray();
        if (upload.Assembled.Length != upload.SizeBytes)
            return StorageError(upload, $"assembled {upload.Assembled.Length} bytes, the session was for {upload.SizeBytes}");

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<CompleteMultipartUploadResult/>", Encoding.UTF8, "application/xml")
        };
    }

    private HttpResponseMessage StorageError(FakeUpload upload, string message)
    {
        Violations.Enqueue($"multipart upload {upload.Id}: {message}");
        return new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent($"<Error><Message>{message}</Message></Error>", Encoding.UTF8, "application/xml")
        };
    }

    // ---- GraphQL ----

    private HttpResponseMessage GraphQLRequest(byte[]? body)
    {
        var request = JsonNode.Parse(body!)!.AsObject();
        var query = request["query"]!.GetValue<string>();
        var variables = request["variables"] as JsonObject;

        var problems = GraphQLContract.Validate(GraphQL.Value, query);
        if (problems.Count > 0)
        {
            foreach (var p in problems) Violations.Enqueue($"GraphQL query rejected: {p}");
            return Json(HttpStatusCode.OK, new JsonObject
            {
                ["errors"] = new JsonArray(problems.Select(p => (JsonNode) new JsonObject
                {
                    ["message"] = p, ["extensions"] = new JsonObject {["code"] = "undefinedField"}
                }).ToArray())
            });
        }

        try
        {
            return Json(HttpStatusCode.OK, GraphQLContract.Execute(query, variables, Resolve));
        }
        catch (GraphQLError error)
        {
            return Json(HttpStatusCode.OK, new JsonObject
            {
                ["errors"] = new JsonArray(new JsonObject
                {
                    ["message"] = error.Message, ["extensions"] = new JsonObject {["code"] = error.Code}
                }),
                ["data"] = null
            });
        }
    }

    private JsonNode? Resolve(string field, IReadOnlyDictionary<string, JsonNode?> args)
    {
        string? Arg(string name) => args.TryGetValue(name, out var v) ? v?.ToString() : null;

        switch (field)
        {
            case "collection":
                return CollectionJson(Find(Arg("slug")));
            case "collectionRevision":
            {
                var collection = Find(Arg("slug"));
                if (Arg("domainName") is { } domain && domain != collection.DomainName)
                    throw new GraphQLError("Collection not found", "NOT_FOUND");
                var revision = Arg("revision") is { } r
                    ? collection.Revisions.FirstOrDefault(x => x.Number == int.Parse(r))
                    : collection.Revisions.LastOrDefault();
                if (revision == null) throw new GraphQLError("Revision not found", "NOT_FOUND");
                return new JsonObject
                {
                    ["id"] = int.Parse(revision.Id),
                    ["revisionNumber"] = revision.Number,
                    ["collectionId"] = int.Parse(collection.Id),
                    ["downloadLink"] = $"/v1/games/{collection.DomainName}/collections/{collection.Slug}/revisions/{revision.Number}/download_link.json",
                    ["collection"] = CollectionJson(collection)
                };
            }
            case "myCollections":
            {
                List<FakeCollection> mine;
                lock (Collections) mine = Collections.AsEnumerable().Reverse().ToList();
                return new JsonObject {["nodes"] = new JsonArray(mine.Select(c => (JsonNode) CollectionJson(c)).ToArray())};
            }
            default:
                throw new GraphQLError($"The fake does not answer {field}", "undefinedField");
        }
    }

    private FakeCollection Find(string? slug)
    {
        lock (Collections)
            return Collections.FirstOrDefault(c => c.Slug == slug)
                   ?? throw new GraphQLError("Collection not found", "NOT_FOUND");
    }

    private static JsonObject CollectionJson(FakeCollection c)
    {
        return new JsonObject
        {
            ["id"] = int.Parse(c.Id),
            ["slug"] = c.Slug,
            ["name"] = c.Name,
            ["summary"] = c.Summary,
            ["description"] = c.Description,
            ["draftRevisionNumber"] = c.Revisions.Count == 0 ? null : c.Revisions[^1].Number,
            ["game"] = new JsonObject {["id"] = 1704, ["name"] = "Skyrim Special Edition", ["domainName"] = c.DomainName}
        };
    }

    // ---- v1 download link ----

    /// <summary>
    ///     The v1 endpoint a revision's <c>downloadLink</c> points at. It is outside the v3 contract (v1 has its own
    ///     older specification), so its answer is the shape Nexus has served for years: a list of mirrors with a
    ///     <c>URI</c> each.
    /// </summary>
    private HttpResponseMessage DownloadLink(Uri url)
    {
        var match = Regex.Match(url.AbsolutePath, @"^/v1/games/[^/]+/collections/(?<slug>[^/]+)/revisions/(?<rev>\d+)/download_link\.json$");
        if (!match.Success || !RevisionArchives.ContainsKey((match.Groups["slug"].Value, int.Parse(match.Groups["rev"].Value))))
            return Json(HttpStatusCode.NotFound, new JsonObject {["message"] = "Not found"});
        return Json(HttpStatusCode.OK, new JsonObject
        {
            ["download_links"] = new JsonArray(new JsonObject
            {
                ["name"] = "Nexus CDN", ["short_name"] = "Nexus CDN",
                ["URI"] = $"https://{StorageHost}/collections/{match.Groups["slug"].Value}/{match.Groups["rev"].Value}.7z"
            })
        });
    }

    // ---- helpers ----

    private HttpResponseMessage Violation(HttpStatusCode status, string message)
    {
        Violations.Enqueue(message);
        return Json(status, ProblemBody(status, message), "application/problem+json");
    }

    private static HttpResponseMessage Problem(HttpStatusCode status, string detail)
    {
        return Json(status, ProblemBody(status, detail), "application/problem+json");
    }

    private static JsonObject ProblemBody(HttpStatusCode status, string detail)
    {
        return new JsonObject
        {
            ["type"] = "about:blank", ["title"] = status.ToString(), ["status"] = (int) status,
            ["detail"] = detail, ["instance"] = "urn:fake-nexus:problem"
        };
    }

    private static JsonObject ValidationBody(JsonArray errors)
    {
        var body = ProblemBody(HttpStatusCode.UnprocessableEntity, "Validation failed");
        body["errors"] = errors;
        return body;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, JsonNode body, string mediaType = "application/json")
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, mediaType)
        };
    }

    private sealed class GraphQLError(string message, string code) : Exception(message)
    {
        public string Code { get; } = code;
    }

    private sealed class FixedLogin(NexusOAuthState state) : ITokenProvider<NexusOAuthState>
    {
        public ValueTask<NexusOAuthState?> Get() => ValueTask.FromResult<NexusOAuthState?>(state);
        public ValueTask SetToken(NexusOAuthState val) => ValueTask.CompletedTask;
        public ValueTask<bool> Delete() => ValueTask.FromResult(false);
        public bool HaveToken() => true;
    }
}

public class FakeUpload
{
    public Guid Id { get; init; }
    public long SizeBytes { get; init; }
    public string Filename { get; init; } = "";
    public long PartSize { get; init; }
    public ConcurrentDictionary<int, byte[]> Parts { get; } = new();
    public byte[]? Assembled { get; set; }
    public string State { get; set; } = "created";
    public bool Finalised { get; set; }
    public bool Claimed { get; set; }
    public int Polls { get; set; }
}

public class FakeCollection
{
    public string Id { get; init; } = "";
    public string Slug { get; init; } = "";
    public string Name { get; init; } = "";
    public string DomainName { get; set; } = "";
    public string Summary { get; init; } = "";
    public string Description { get; init; } = "";
    public List<FakeRevision> Revisions { get; } = new();
}

public class FakeRevision
{
    public string Id { get; init; } = "";
    public int Number { get; init; }
    public FakeUpload? Upload { get; init; }
    public JsonObject? Payload { get; init; }
}
