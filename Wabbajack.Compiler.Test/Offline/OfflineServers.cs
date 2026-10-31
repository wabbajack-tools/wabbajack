using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClientConfiguration = Wabbajack.Networking.WabbajackClientApi.Configuration;

namespace Wabbajack.Compiler.Test.Offline;

/// <summary>
///     Stands in for the Wabbajack build server and the GitHub-hosted lists a compile reads, so the compiler
///     tests run without a network. Each endpoint a compile uses gets the smallest answer that lets it carry on:
///     an allow-list that admits any https URL, no mirrors, no indexed game files, and an empty success for
///     metrics and the uploaded list definition, which went to the production server before this existed.
///     The generated mod archive is served at <see cref="FixtureUrl" /> for the compiler's link check.
///     Anything else throws with the URL in the message, so a test that starts to need the network fails
///     where it can be seen rather than reaching out.
/// </summary>
public class OfflineServers : HttpMessageHandler
{
    private readonly ClientConfiguration _configuration;

    public OfflineServers(ClientConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>Where the <see cref="GeneratedFixture" /> archive is "downloaded" from.</summary>
    public static readonly Uri FixtureUrl = new("https://fixture.invalid/" + Uri.EscapeDataString(GeneratedFixture.FileName));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var url = request.RequestUri!;
        if (url == FixtureUrl)
        {
            var content = new ByteArrayContent(await GeneratedFixture.Bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            return new HttpResponseMessage(HttpStatusCode.OK) {Content = content};
        }

        return await Endpoint(url);
    }

    private Task<HttpResponseMessage> Endpoint(Uri url)
    {
        var buildServer = _configuration.BuildServerUrl.ToString();

        if (url == _configuration.ServerAllowList || url == _configuration.MirrorAllowList)
            return Answer("AllowedPrefixes:\n  - https://\nGoogleIDs: []\n");
        if (url == _configuration.MirrorList || url == _configuration.UpgradedArchives)
            return Answer("[]");
        if (url.Host == "raw.githubusercontent.com" && url.AbsolutePath.StartsWith("/wabbajack-tools/indexed-game-files/"))
            return Answer("[]");
        if (url.ToString().StartsWith(buildServer + "metrics/") ||
            url.ToString() == buildServer + "list_definitions/ingest")
            return Answer("");
        if (url.ToString().StartsWith(buildServer + "mod_files/by_hash/"))
            return Answer("[]");

        throw new HttpRequestException($"Offline test tried to reach {url}");
    }

    private static Task<HttpResponseMessage> Answer(string body)
    {
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8)
        });
    }
}
