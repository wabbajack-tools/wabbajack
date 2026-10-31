using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.Downloaders.GameFile;
using Wabbajack.DTOs;
using Wabbajack.Networking.Bethesda.Steam;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Networking.Bethesda.Test;

/// <summary>
///     The Bethesda API as a script: answers <see cref="BethesdaApiClient.SignIn" /> and
///     <see cref="BethesdaApiClient.Resolve" /> without a ticket or a network, and counts what it was asked.
/// </summary>
internal sealed class FakeApi : BethesdaApiClient
{
    public FakeApi() : base(new HttpClient(), new NoSteamTicket(), NullLogger<BethesdaApiClient>.Instance)
    {
    }

    public List<CkmSlot> Slots { get; } = new();
    public List<long> Asked { get; } = new();
    public int SignIns { get; private set; }
    public int Resolves { get; private set; }

    /// <summary>Thrown out of every sign in while set.</summary>
    public Func<Exception>? SignInThrows { get; set; }

    /// <summary>Thrown out of the next resolve, then cleared, so a later file can succeed.</summary>
    public Func<Exception>? ResolveThrowsOnce { get; set; }

    public override Task SignIn(CancellationToken token = default)
    {
        SignIns++;
        if (SignInThrows != null) throw SignInThrows();
        return Task.CompletedTask;
    }

    public override Task<IReadOnlyList<CkmSlot>> Resolve(IEnumerable<long> contentIds,
        CancellationToken token = default)
    {
        Resolves++;
        Asked.AddRange(contentIds);

        if (ResolveThrowsOnce is { } thrown)
        {
            ResolveThrowsOnce = null;
            throw thrown();
        }

        return Task.FromResult<IReadOnlyList<CkmSlot>>(Slots.ToArray());
    }
}

/// <summary>
///     Serves one container, and counts how many times it was asked for it. Optionally holds every request
///     at a gate, so a test can have two callers in flight at once.
/// </summary>
internal sealed class FakeDownloads : HttpMessageHandler
{
    private int _requests;

    public byte[] Body { get; set; } = Array.Empty<byte>();
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public int Requests => Volatile.Read(ref _requests);

    /// <summary>When set, every request waits here until the test releases it.</summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>Completed when the first request arrives.</summary>
    public TaskCompletionSource FirstRequest { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The header names each request carried, lower-cased, with their values.</summary>
    public List<Dictionary<string, string>> Headers { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requests);
        lock (Headers)
        {
            Headers.Add(request.Headers.ToDictionary(h => h.Key.ToLowerInvariant(), h => string.Join(" ", h.Value)));
        }

        FirstRequest.TrySetResult();
        if (Gate != null) await Gate.Task.WaitAsync(cancellationToken);

        return new HttpResponseMessage(Status) {Content = new ByteArrayContent(Body)};
    }
}

internal sealed class FakeGameLocator : IGameLocator
{
    public bool Installed { get; set; }

    public AbsolutePath GameLocation(Game game)
    {
        return KnownFolders.EntryPoint;
    }

    public bool IsInstalled(Game game)
    {
        return Installed;
    }

    public bool TryFindLocation(Game game, out AbsolutePath path)
    {
        path = Installed ? KnownFolders.EntryPoint : default;
        return Installed;
    }

    public bool TryGetSteamBuildId(Game game, out string buildId)
    {
        buildId = string.Empty;
        return false;
    }

    public bool TryGetSteamManifests(Game game, out SteamManifest[] manifests)
    {
        manifests = Array.Empty<SteamManifest>();
        return false;
    }
}

internal sealed class FakeSteamPresence : ISteamClientPresence
{
    public bool IsRunning { get; set; }
}

/// <summary>A ticket source with no Steam behind it, for the paths that must never ask for one.</summary>
internal sealed class NoSteamTicket : ISteamAppTicketSource
{
    public ValueTask<byte[]> GetEncryptedAppTicket(uint appId, CancellationToken token = default)
    {
        throw new SteamAppTicketException(SteamAppTicketError.SteamNotRunning, "No Steam here.");
    }
}

/// <summary>A ticket source that always hands over the same bytes, and records which app it was asked for.</summary>
internal sealed class FixedTicket : ISteamAppTicketSource
{
    private readonly byte[] _ticket;

    public FixedTicket(params byte[] ticket)
    {
        _ticket = ticket;
    }

    public List<uint> AskedFor { get; } = new();

    public ValueTask<byte[]> GetEncryptedAppTicket(uint appId, CancellationToken token = default)
    {
        AskedFor.Add(appId);
        return ValueTask.FromResult(_ticket);
    }
}

/// <summary>One request <see cref="ScriptedBethesda" /> saw, copied out before the client disposed it.</summary>
internal sealed record SeenRequest(HttpMethod Method, Uri Uri, IReadOnlyDictionary<string, string> Headers,
    string? Body, string? ContentType);

/// <summary>
///     <c>api.bethesda.net</c> as a queue of answers, one per request, in order. Running off the end of the
///     queue fails the test rather than inventing an answer.
/// </summary>
internal sealed class ScriptedBethesda : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _answers = new();

    public List<SeenRequest> Requests { get; } = new();

    public ScriptedBethesda Then(HttpStatusCode status, string body = "")
    {
        _answers.Enqueue(() => new HttpResponseMessage(status) {Content = new StringContent(body)});
        return this;
    }

    public ScriptedBethesda ThenOk(string body)
    {
        return Then(HttpStatusCode.OK, body);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new SeenRequest(request.Method, request.RequestUri!,
            request.Headers.ToDictionary(h => h.Key.ToLowerInvariant(), h => string.Join(" ", h.Value)),
            body, request.Content?.Headers.ContentType?.MediaType));

        if (_answers.Count == 0)
            throw new InvalidOperationException(
                $"Nothing scripted for request {Requests.Count}: {request.Method} {request.RequestUri}");

        return _answers.Dequeue()();
    }
}
