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
using Wabbajack.Installer.Preflight.Rules;
using Wabbajack.Installer.Test.Preflight.Fakes;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.NexusApi;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.RateLimiter;
using Xunit;
using ClientConfiguration = Wabbajack.Networking.WabbajackClientApi.Configuration;

namespace Wabbajack.Installer.Test.Preflight;

/// <summary>
///     What a finished install reports. The install has already succeeded by the time any of this runs, so the
///     property that matters as much as the URLs is that nothing here can turn it into a failure.
/// </summary>
public class FinishInstallMetricsTests
{
    private static readonly string Metrics = new ClientConfiguration().BuildServerUrl + "metrics/";

    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

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
        public Func<HttpRequestMessage, Task<HttpResponseMessage>>? Answer { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests.Enqueue(request.RequestUri!.AbsoluteUri);
            return Answer?.Invoke(request) ?? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class ThrowingProbe : INexusLoginProbe
    {
        public Task<NexusLoginStatus> Probe(CancellationToken token) => throw new HttpRequestException("offline");
    }

    private sealed class HangingProbe : INexusLoginProbe
    {
        public async Task<NexusLoginStatus> Probe(CancellationToken token)
        {
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException("unreachable");
        }
    }

    private static Client Client(Server server)
    {
        return new Client(NullLogger<Client>.Instance, new HttpClient(server), new Keys(), null!,
            new Resource<HttpClient>("http", 4), new Resource<Client>("hash", 4), new ClientConfiguration());
    }

    private static Task Send(Server server, INexusLoginProbe probe, TimeSpan? limit = null)
    {
        return new FinishInstallMetrics(Client(server), probe, NullLogger.Instance, limit ?? Limit).Send("Some List");
    }

    [Theory]
    [InlineData(true, true, "premium")]
    [InlineData(true, false, "free")]
    public async Task ALoginIsReportedByItsTier(bool loggedIn, bool premium, string tier)
    {
        var server = new Server();
        var probe = new FakeNexusLoginProbe
        {
            Status = new NexusLoginStatus(loggedIn, premium, "someone", null, NexusCredentialSource.OAuth)
        };

        await Send(server, probe);

        Assert.Equal(new[]
        {
            $"{Metrics}finish_install/Some%20List",
            $"{Metrics}finish_install_{tier}/Some%20List"
        }, server.Requests);
    }

    [Fact]
    public async Task NoLoginIsReportedAsLoggedOut()
    {
        var server = new Server();

        await Send(server, new FakeNexusLoginProbe());

        Assert.Contains($"{Metrics}finish_install_logged_out/Some%20List", server.Requests);
    }

    [Fact]
    public async Task AProbeThatThrowsIsReportedAsUnknown()
    {
        var server = new Server();

        await Send(server, new ThrowingProbe());

        Assert.Contains($"{Metrics}finish_install_unknown/Some%20List", server.Requests);
    }

    [Fact]
    public async Task AnUnreachableServerDoesNotFailTheInstall()
    {
        var server = new Server {Answer = _ => throw new HttpRequestException("offline")};

        await Send(server, new FakeNexusLoginProbe());

        Assert.Equal(2, server.Requests.Count);
    }

    /// <summary>
    ///     A probe stuck on a dead connection cannot hold a finished install open, and neither ping is lost to it.
    /// </summary>
    [Fact]
    public async Task AHungProbeGivesUpWithinTheLimit()
    {
        var server = new Server();

        await Send(server, new HangingProbe(), TimeSpan.FromMilliseconds(100)).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(new[]
        {
            $"{Metrics}finish_install/Some%20List",
            $"{Metrics}finish_install_unknown/Some%20List"
        }, server.Requests);
    }

    /// <summary>
    ///     The tier's ping must not be able to take the process down: a 500 on the rebound path ends in
    ///     <c>Environment.Exit</c>, and the user has a finished install on screen at this point.
    /// </summary>
    [Fact]
    public async Task ARefusedTierPingLeavesTheProcessRunning()
    {
        var server = new Server
        {
            Answer = request => Task.FromResult(new HttpResponseMessage(
                request.RequestUri!.AbsoluteUri.Contains("finish_install_")
                    ? HttpStatusCode.InternalServerError
                    : HttpStatusCode.OK))
        };

        await Send(server, new FakeNexusLoginProbe());

        Assert.Equal(2, server.Requests.Count(r => r.Contains("finish_install")));
    }
}
