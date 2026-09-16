using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Wabbajack.Networking.Bethesda;

/// <summary>
///     The chain against <c>api.bethesda.net</c> that turns a Steam ticket into download URLs for
///     Anniversary Edition Creations. Three calls and no password anywhere:
///     <list type="number">
///         <item><c>POST /session/external-login</c> exchanges the ticket for a session token;</item>
///         <item><c>POST /vccs/fulfillment/update_first_party_entitlements</c> turns the Steam ownership the ticket states into Bethesda entitlements;</item>
///         <item><c>GET /ugcmods/content/v1/content</c> resolves content ids to records carrying presigned <c>.ckm</c> URLs.</item>
///     </list>
///     <para>
///         Nothing here decides ownership. The ticket does, server-side, which is why the client can be
///         this thin: a request either comes back with URLs or it does not.
///     </para>
/// </summary>
public class BethesdaApiClient
{
    /// <summary>Statuses worth trying again. A 4xx is an answer, not a hiccup, so none of them are here.</summary>
    private static readonly HashSet<HttpStatusCode> Retryable =
    [
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.InternalServerError,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout
    ];

    private const int MaxAttempts = 4;

    private readonly HttpClient _client;
    private readonly ILogger<BethesdaApiClient> _logger;
    private readonly ISteamAppTicketSource _tickets;

    public BethesdaApiClient(HttpClient client, ISteamAppTicketSource tickets, ILogger<BethesdaApiClient> logger)
    {
        _client = client;
        _tickets = tickets;
        _logger = logger;
    }

    /// <summary>The session token, once one has been obtained. Never logged, never shown.</summary>
    private string? SessionToken { get; set; }

    /// <summary>Whether <see cref="SignIn" /> has succeeded.</summary>
    public bool HasSession => !string.IsNullOrEmpty(SessionToken);

    /// <summary>The Bethesda.net account name the ticket turned out to belong to. For messages only.</summary>
    public string? Username { get; private set; }

    /// <summary>
    ///     Mints a ticket, exchanges it for a session and fulfills entitlements from it. The ticket is used
    ///     twice and kept no longer than that.
    /// </summary>
    public virtual async Task SignIn(CancellationToken token = default)
    {
        var ticket = await _tickets.GetEncryptedAppTicket(BethesdaConstants.SkyrimSpecialEditionAppId, token);
        if (ticket.Length == 0)
            throw new BethesdaApiException("Steam returned an empty encrypted app ticket.",
                HttpStatusCode.Unauthorized);

        var hex = ToHex(ticket);

        using (var login = await Post("/session/external-login", new Dictionary<string, object>
               {
                   ["scheme"] = "steam",
                   ["token"] = hex,
                   ["language"] = BethesdaConstants.DefaultLanguage
               }, token))
        {
            var response = Unwrap(login, "/session/external-login");
            SessionToken = response.TryGetProperty("session_token", out var session) &&
                           session.ValueKind == JsonValueKind.String
                ? session.GetString()
                : null;

            if (string.IsNullOrEmpty(SessionToken))
                throw new BethesdaApiException("Bethesda accepted the Steam ticket but returned no session token.",
                    HttpStatusCode.OK);

            Username = response.TryGetProperty("username", out var username) &&
                       username.ValueKind == JsonValueKind.String
                ? username.GetString()
                : null;
        }

        _logger.LogInformation("Signed in to Bethesda.net as {Username} with a Steam app ticket", Username ?? "an unnamed account");

        using var fulfillment = await Post("/vccs/fulfillment/update_first_party_entitlements",
            new Dictionary<string, object> {["token"] = hex}, token);
        Unwrap(fulfillment, "/vccs/fulfillment/update_first_party_entitlements");
    }

    /// <summary>
    ///     Resolves content ids to the best <c>.ckm</c> for each. An id that comes back with no record is
    ///     simply absent from the answer, which is what a Creation the account does not own is expected to
    ///     look like - the caller decides what to make of that rather than this assuming ownership. Whether
    ///     the endpoint filters by entitlement at all is untested: the one live run was on an account that
    ///     owns the Anniversary Upgrade, and it returned all 74.
    /// </summary>
    public virtual async Task<IReadOnlyList<CkmSlot>> Resolve(IEnumerable<long> contentIds,
        CancellationToken token = default)
    {
        var ids = contentIds.Distinct().ToArray();
        if (ids.Length == 0) return Array.Empty<CkmSlot>();
        if (!HasSession)
            throw new BethesdaApiException("Resolving Creations needs a session; call SignIn first.",
                HttpStatusCode.Unauthorized);

        using var document = await Send(HttpMethod.Get, ContentQuery(ids), null, token);
        var response = Unwrap(document, "/ugcmods/content/v1/content");

        if (!response.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            throw new BethesdaApiException("The content response carried no data array.", HttpStatusCode.OK);

        return CkmDownload.BestPerContent(data.EnumerateArray());
    }

    /// <summary>
    ///     The resolve URL. Matrix parameters, semicolon-separated: <c>&amp;</c> is not what this endpoint
    ///     parses, and the path is <c>content/v1/content</c> rather than the <c>v1/content</c> that the
    ///     unrelated search endpoint lives at. <c>download=true</c> is what makes the records carry URLs at
    ///     all, and <c>size</c> is the page size, so it has to be at least as large as the id list.
    /// </summary>
    public static Uri ContentQuery(IReadOnlyCollection<long> contentIds, int? size = null)
    {
        var ids = string.Join(",", contentIds);
        var page = size ?? contentIds.Count + 5;
        return new Uri(BethesdaConstants.ApiBase,
            $"/ugcmods/content/v1/content?product={BethesdaConstants.Product};content_ids={ids};download=true;size={page}");
    }

    /// <summary>
    ///     The ticket as the <c>token</c> field wants it: hex. Base64 is refused with the same
    ///     <c>412 / 14029</c> a garbage ticket gets, which reads as "your ticket is bad" and is really
    ///     "your encoding is", so this is deliberately the only way a ticket is written down here.
    /// </summary>
    public static string ToHex(ReadOnlySpan<byte> ticket)
    {
        return Convert.ToHexString(ticket).ToLowerInvariant();
    }

    private Task<JsonDocument> Post(string path, Dictionary<string, object> body, CancellationToken token)
    {
        return Send(HttpMethod.Post, new Uri(BethesdaConstants.ApiBase, path), JsonSerializer.Serialize(body), token);
    }

    /// <summary>
    ///     One call, with the retries a transient answer earns. The request is rebuilt per attempt rather
    ///     than cloned: an <see cref="HttpRequestMessage" /> cannot be sent twice, and rebuilding it is also
    ///     what keeps the body out of a buffer nothing else needs.
    /// </summary>
    private async Task<JsonDocument> Send(HttpMethod method, Uri uri, string? body, CancellationToken token)
    {
        var delay = TimeSpan.FromSeconds(1);

        for (var attempt = 1;; attempt++)
        {
            using var message = new HttpRequestMessage(method, uri);
            BethesdaHeaders.Apply(message, BethesdaHeaders.Build(SessionToken));
            if (body is not null)
            {
                message.Content = new StringContent(body, Encoding.UTF8);
                message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }

            using var response = await _client.SendAsync(message, token);

            if (Retryable.Contains(response.StatusCode) && attempt < MaxAttempts)
            {
                _logger.LogDebug("{Method} {Path} answered {Status}; retrying in {Delay}",
                    method, uri.AbsolutePath, (int) response.StatusCode, delay);
                await Task.Delay(delay, token);
                delay *= 2;
                continue;
            }

            var content = await response.Content.ReadAsStringAsync(token);
            if (!response.IsSuccessStatusCode) throw Refusal(method, uri, response.StatusCode, content);

            try
            {
                return JsonDocument.Parse(content);
            }
            catch (JsonException e)
            {
                throw new BethesdaApiException(
                    $"{method} {uri.AbsolutePath} answered {(int) response.StatusCode} with a body that is not JSON: {e.Message}",
                    response.StatusCode);
            }
        }
    }

    /// <summary>
    ///     Turns a refusal into something worth reading, and never puts the body in a log line: an error
    ///     body is the origin's and carries nothing secret, but the request that produced it carried both
    ///     the key and the session token, so nothing about a request is echoed beyond its method and path.
    /// </summary>
    private BethesdaApiException Refusal(HttpMethod method, Uri uri, HttpStatusCode status, string body)
    {
        var where = $"{method} {uri.AbsolutePath}";
        var (code, platformMessage) = PlatformError(body);

        _logger.LogWarning("{Where} answered {Status} (platform code {Code})", where, (int) status, code);

        var explanation = code switch
        {
            null =>
                $"{where} was blocked before it reached Bethesda ({(int) status}). The x-bnet-key this build carries is no longer one the edge recognises.",
            5001 =>
                $"{where} was refused for its headers ({(int) status}, {code}: {platformMessage}).",
            14029 =>
                $"{where} could not verify the Steam ticket ({(int) status}, {code}: {platformMessage}). Steam has to be running and logged in on an account that owns Skyrim Special Edition.",
            _ => $"{where} answered {(int) status} ({code}: {platformMessage})."
        };

        return new BethesdaApiException(explanation, status, code, platformMessage);
    }

    /// <summary>
    ///     Reads the <c>platform</c> envelope every origin answer is wrapped in. Null means there was no
    ///     envelope, which is how an edge refusal - an empty CloudFront 403 - tells itself apart from
    ///     anything the origin said.
    /// </summary>
    public static (int? Code, string? Message) PlatformError(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return (null, null);

        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("platform", out var platform) ||
                platform.ValueKind != JsonValueKind.Object)
                return (null, null);

            var code = platform.TryGetProperty("code", out var codeValue) && codeValue.TryGetInt32(out var parsed)
                ? parsed
                : (int?) null;
            var message = platform.TryGetProperty("message", out var messageValue) &&
                          messageValue.ValueKind == JsonValueKind.String
                ? messageValue.GetString()
                : null;

            return (code, message);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    /// <summary>
    ///     The <c>platform.response</c> out of a 200. A 200 still carries a platform code, so a body that
    ///     succeeded at the HTTP layer and failed at the origin's is caught here rather than read as an
    ///     empty answer.
    /// </summary>
    private static JsonElement Unwrap(JsonDocument document, string where)
    {
        if (!document.RootElement.TryGetProperty("platform", out var platform) ||
            platform.ValueKind != JsonValueKind.Object)
            throw new BethesdaApiException($"{where} answered without a platform envelope.", HttpStatusCode.OK);

        var code = platform.TryGetProperty("code", out var codeValue) && codeValue.TryGetInt32(out var parsed)
            ? parsed
            : (int?) null;
        var message = platform.TryGetProperty("message", out var messageValue) &&
                      messageValue.ValueKind == JsonValueKind.String
            ? messageValue.GetString()
            : null;

        if (!platform.TryGetProperty("response", out var response) || response.ValueKind != JsonValueKind.Object)
            throw new BethesdaApiException($"{where} answered {code}: {message}", HttpStatusCode.OK, code, message);

        return response;
    }
}
