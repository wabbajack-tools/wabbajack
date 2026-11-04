using System;
using System.Collections.Concurrent;
using System.CommandLine;
using System.CommandLine.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using Wabbajack.CLI.Verbs;
using Wabbajack.DTOs.Interventions;
using Wabbajack.DTOs.Logins;
using Wabbajack.Networking.Http;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;
using ClientConfiguration = Wabbajack.Networking.WabbajackClientApi.Configuration;

namespace Wabbajack.CLI.Test.Offline;

/// <summary>
///     A CLI service container that never reaches the network: every HTTP request, through the shared client or
///     the named one downloads use, goes to <see cref="Servers" />. It registers the verbs it is used with
///     directly rather than through <c>AddCLIVerbs</c>, whose registry is static and would collect a second set
///     beside <see cref="CLITestFixture" />'s.
/// </summary>
public sealed class OfflineCliHost : IDisposable
{
    private readonly IHost _host;

    public OfflineCliHost()
    {
        _host = Host.CreateDefaultBuilder(Array.Empty<string>())
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddProvider(new OutputLogger(this));
            })
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton(new JsonSerializerOptions());
                services.AddResumableHttpDownloader();
                services.AddSingleton<IConsole, SystemConsole>();
                services.AddSingleton<TemporaryFileManager>();
                services.AddSingleton<FileExtractor.FileExtractor>();
                services.AddSingleton(new ParallelOptions {MaxDegreeOfParallelism = Environment.ProcessorCount});
                services.AddSingleton<IUserInterventionHandler, ThrowingUserInterventionHandler>();
                services.AddOSIntegrated(o =>
                {
                    o.UseLocalCache = true;
                    o.UseStubbedGameFolders = true;
                });

                services.AddSingleton(s => new OfflineServers(s.GetRequiredService<ClientConfiguration>()));
                services.AddSingleton(s => new HttpClient(s.GetRequiredService<OfflineServers>(), false));
                services.AddSingleton<IHttpClientFactory>(s => s.GetRequiredService<OfflineServers>());
                services.AddSingleton<ITokenProvider<WabbajackApiState>, FixedApiToken>();

                services.AddTransient<Compile>();
                services.AddTransient<Install>();
                services.AddTransient<VerifyModlistInstall>();
            }).Build();
    }

    /// <summary>Where the verbs' logs go: the running test's output, so a failed preflight says why.</summary>
    public ITestOutputHelper? Output { get; set; }

    public IServiceProvider Services => _host.Services;
    public OfflineServers Servers => Services.GetRequiredService<OfflineServers>();

    public void Dispose()
    {
        _host.Dispose();
    }

    private sealed class OutputLogger(OfflineCliHost host) : ILoggerProvider, ILogger
    {
        public ILogger CreateLogger(string categoryName) => this;
        public void Dispose() { }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            try
            {
                host.Output?.WriteLine($"{logLevel}: {formatter(state, exception)}{(exception == null ? "" : " " + exception)}");
            }
            catch (InvalidOperationException)
            {
                // Logged after the test finished; nowhere to write it.
            }
        }
    }

    private sealed class FixedApiToken : ITokenProvider<WabbajackApiState>
    {
        public ValueTask<WabbajackApiState?> Get() =>
            ValueTask.FromResult<WabbajackApiState?>(new WabbajackApiState {MetricsKey = "offline-cli-tests"});

        public ValueTask SetToken(WabbajackApiState val) => ValueTask.CompletedTask;
        public ValueTask<bool> Delete() => ValueTask.FromResult(false);
        public bool HaveToken() => true;
    }
}

/// <summary>
///     Stands in for the build server and the GitHub-hosted lists a compile and an install read, with the
///     smallest answer that lets them carry on, and serves whatever files a test has put in <see cref="Files" />.
///     Any other request throws with its URL, so a verb that starts to need the network fails visibly.
/// </summary>
public sealed class OfflineServers : HttpMessageHandler, IHttpClientFactory
{
    private readonly ClientConfiguration _configuration;

    public OfflineServers(ClientConfiguration configuration)
    {
        _configuration = configuration;
    }

    public ConcurrentDictionary<Uri, byte[]> Files { get; } = new();
    public ConcurrentQueue<Uri> Requested { get; } = new();

    public HttpClient CreateClient(string name) => new(this, false);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var url = request.RequestUri!;
        Requested.Enqueue(url);

        if (Files.TryGetValue(new Uri(url.GetLeftPart(UriPartial.Path)), out var bytes))
        {
            // The resumable downloader always asks for a range, from 0 on a fresh download.
            var from = (int) (request.Headers.Range?.Ranges.FirstOrDefault()?.From ?? 0);
            var content = new ByteArrayContent(bytes[from..]);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            content.Headers.ContentLength = bytes.Length - from;
            return Task.FromResult(new HttpResponseMessage(from > 0 ? HttpStatusCode.PartialContent : HttpStatusCode.OK)
            {
                Content = content
            });
        }

        var buildServer = _configuration.BuildServerUrl.ToString();
        var text = url.ToString();
        string? body = null;
        if (url == _configuration.ServerAllowList || url == _configuration.MirrorAllowList)
            body = "AllowedPrefixes:\n  - https://\nGoogleIDs: []\n";
        else if (url == _configuration.MirrorList || url == _configuration.UpgradedArchives)
            body = "[]";
        else if (url.Host == "raw.githubusercontent.com" && url.AbsolutePath.StartsWith("/wabbajack-tools/indexed-game-files/"))
            body = "[]";
        else if (text.StartsWith(buildServer + "metrics/") || text == buildServer + "list_definitions/ingest")
            body = "";
        else if (text.StartsWith(buildServer + "mod_files/by_hash/"))
            body = "[]";

        if (body == null)
            throw new HttpRequestException($"Offline CLI test tried to reach {url}");

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8)
        });
    }
}
