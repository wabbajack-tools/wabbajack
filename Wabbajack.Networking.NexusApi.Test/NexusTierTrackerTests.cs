#nullable enable
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.DTOs.Logins;
using Wabbajack.Installer.Preflight;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated.Preflight;
using Xunit;
using ClientConfiguration = Wabbajack.Networking.WabbajackClientApi.Configuration;

namespace Wabbajack.Networking.NexusApi.Test;

/// <summary>
///     A conversion is only visible across runs, so most of these make a second tracker over the same folder:
///     that is what the next launch of the app looks like.
/// </summary>
public sealed class NexusTierTrackerTests : IAsyncLifetime
{
    private static readonly string Metrics = new ClientConfiguration().BuildServerUrl + "metrics/";
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    private static readonly NexusLoginStatus Premium = new(true, true, "someone", null, NexusCredentialSource.OAuth);
    private static readonly NexusLoginStatus Free = new(true, false, "someone", null, NexusCredentialSource.OAuth);
    private static readonly NexusLoginStatus LoggedOut = new(false, false, null, null, NexusCredentialSource.None);

    private readonly TemporaryFileManager _manager = new();
    private TemporaryPath _folder;

    public Task InitializeAsync()
    {
        _folder = _manager.CreateFolder();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _folder.DisposeAsync();
    }

    private sealed class Keys : ITokenProvider<WabbajackApiState>
    {
        public ValueTask<WabbajackApiState?> Get()
        {
            return new ValueTask<WabbajackApiState?>(new WabbajackApiState {MetricsKey = "metrics-key"});
        }

        public ValueTask SetToken(WabbajackApiState val) => throw new NotSupportedException();
        public ValueTask<bool> Delete() => throw new NotSupportedException();
        public bool HaveToken() => true;
    }

    private sealed class Server : HttpMessageHandler
    {
        public ConcurrentQueue<string> Requests { get; } = new();
        public Func<Task<HttpResponseMessage>>? Answer { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests.Enqueue(request.RequestUri!.AbsoluteUri);
            return Answer?.Invoke() ?? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class Probe : INexusLoginProbe
    {
        public NexusLoginStatus Status { get; set; } = LoggedOut;

        Task<NexusLoginStatus> INexusLoginProbe.Probe(CancellationToken token)
        {
            return Task.FromResult(Status);
        }
    }

    private NexusTierTracker Tracker(Server server, NexusLoginStatus status, TimeSpan? limit = null)
    {
        var client = new Client(NullLogger<Client>.Instance, new HttpClient(server), new Keys(), null!,
            new Resource<HttpClient>("http", 4), new Resource<Client>("hash", 4), new ClientConfiguration());
        return new NexusTierTracker(new Probe {Status = status}, client, NullLogger<NexusTierTracker>.Instance,
            _folder.Path, limit ?? Limit);
    }

    private Task See(Server server, NexusLoginStatus status)
    {
        return Tracker(server, status).Probe(CancellationToken.None);
    }

    private AbsolutePath StateFile => _folder.Path.Combine("encrypted", "nexus-state");

    [Fact]
    public async Task HandsBackWhatTheProbeSaid()
    {
        var status = await Tracker(new Server(), Premium).Probe(CancellationToken.None);

        Assert.Same(Premium, status);
    }

    /// <summary>Everyone already premium when this ships must not read as having just converted.</summary>
    [Theory]
    [MemberData(nameof(BothTiers))]
    public async Task TheFirstSightingOnlyRecordsABaseline(NexusLoginStatus status)
    {
        var server = new Server();

        await See(server, status);

        Assert.Empty(server.Requests);
        Assert.True(StateFile.FileExists());
    }

    public static TheoryData<NexusLoginStatus> BothTiers => new() {Premium, Free};

    [Fact]
    public async Task NothingChangedSendsNothing()
    {
        var server = new Server();

        await See(server, Free);
        await See(server, Free);

        Assert.Empty(server.Requests);
    }

    [Fact]
    public async Task UpgradingAcrossRunsIsReported()
    {
        var server = new Server();

        await See(server, Free);
        await See(server, Premium);

        Assert.Equal(new[] {$"{Metrics}nexus_tier_change/free_to_premium"}, server.Requests);
    }

    [Fact]
    public async Task LapsingAcrossRunsIsReported()
    {
        var server = new Server();

        await See(server, Premium);
        await See(server, Free);

        Assert.Equal(new[] {$"{Metrics}nexus_tier_change/premium_to_free"}, server.Requests);
    }

    [Fact]
    public async Task AChangeIsReportedOnce()
    {
        var server = new Server();

        await See(server, Free);
        await See(server, Premium);
        await See(server, Premium);

        Assert.Single(server.Requests);
    }

    /// <summary>Logging out says nothing about the membership, so the answer from before it still stands.</summary>
    [Fact]
    public async Task LoggingOutKeepsTheBaseline()
    {
        var server = new Server();

        await See(server, Free);
        await See(server, LoggedOut);
        await See(server, Premium);

        Assert.Equal(new[] {$"{Metrics}nexus_tier_change/free_to_premium"}, server.Requests);
    }

    [Fact]
    public async Task AnUnansweredLoginWritesNoBaseline()
    {
        await See(new Server(), new NexusLoginStatus(false, false, null, "refused", NexusCredentialSource.OAuth));

        Assert.False(StateFile.FileExists());
    }

    [Fact]
    public async Task APingThatCouldNotBeSentIsTriedAgain()
    {
        var server = new Server();
        await See(server, Free);

        server.Answer = () => throw new HttpRequestException("offline");
        await See(server, Premium);

        server.Answer = null;
        await See(server, Premium);

        Assert.Equal(2, server.Requests.Count);
        Assert.All(server.Requests, r => Assert.Equal($"{Metrics}nexus_tier_change/free_to_premium", r));
    }

    [Fact]
    public async Task AnUnreadableFileIsNoBaseline()
    {
        var server = new Server();
        await See(server, Free);
        await StateFile.WriteAllTextAsync("this was never encrypted json");

        await See(server, Premium);
        await See(server, Free);

        Assert.Equal(new[] {$"{Metrics}nexus_tier_change/premium_to_free"}, server.Requests);
    }

    /// <summary>A 500 on the rebound path ends in <c>Environment.Exit</c>, from somewhere in the middle of a preflight.</summary>
    [Fact]
    public async Task ARefusedPingLeavesTheProcessRunning()
    {
        var server = new Server();
        await See(server, Free);

        server.Answer = () => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        await See(server, Premium);

        Assert.Single(server.Requests);
    }

    /// <summary>The nexus-login row is waiting on this, and it should not wait on the metrics server.</summary>
    [Fact]
    public async Task AHungPingDoesNotHoldTheProbe()
    {
        var server = new Server();
        await See(server, Free);

        var release = new TaskCompletionSource<HttpResponseMessage>();
        server.Answer = () => release.Task;

        var status = await Tracker(server, Premium, TimeSpan.FromMilliseconds(100)).Probe(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Same(Premium, status);
        release.SetResult(new HttpResponseMessage(HttpStatusCode.OK));
    }

    /// <summary>The startup probe and preflight's can land together, and neither may report the change twice.</summary>
    [Fact]
    public async Task ProbesArrivingTogetherReportOnce()
    {
        var server = new Server();
        await See(server, Free);

        var tracker = Tracker(server, Premium);
        await Task.WhenAll(Enumerable.Range(0, 2000)
            .Select(_ => Task.Run(() => tracker.Probe(CancellationToken.None))));

        Assert.Single(server.Requests);
    }
}
