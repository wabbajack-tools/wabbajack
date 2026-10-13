using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.Downloaders.Http;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Validation;
using Wabbajack.Networking.Http;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Xunit;

namespace Wabbajack.Downloaders.Dispatcher.Test.Offline;

/// <summary>
///     The plain-URL downloader against <see cref="FakeHttp" />: how a meta file becomes a state and back, and
///     the checks <see cref="HttpDownloader.Verify" /> makes before a compile or install trusts a link.
/// </summary>
public class HttpDownloaderTests
{
    private static readonly Uri Url = new("https://files.example.com/mods/archive.7z");

    private readonly TemporaryFileManager _manager;

    public HttpDownloaderTests(TemporaryFileManager manager)
    {
        _manager = manager;
    }

    private static HttpDownloader Downloader(FakeHttp http)
    {
        return new HttpDownloader(NullLogger<HttpDownloader>.Instance, http.Client(),
            new ResumableDownloader(NullLogger<ResumableDownloader>.Instance, http));
    }

    private static Archive Archive(long size = 0, params string[] headers)
    {
        return new Archive
        {
            Name = "archive.7z",
            Size = size,
            State = new DTOs.DownloadStates.Http {Url = Url, Headers = headers}
        };
    }

    private static async Task<bool> Verify(FakeHttp http, Archive archive)
    {
        using var job = await new Resource<HttpDownloaderTests>("verify", 1).Begin("verify", 0, CancellationToken.None);
        return await Downloader(http).Verify(archive, (DTOs.DownloadStates.Http) archive.State, job, CancellationToken.None);
    }

    [Fact]
    public void MetaIni_RoundTripsThroughResolve()
    {
        var downloader = Downloader(new FakeHttp(_ => throw new InvalidOperationException()));
        var archive = Archive(0, "Referer:https://example.com/", "X-Token:abc:def");

        var ini = downloader.MetaIni(archive, (DTOs.DownloadStates.Http) archive.State)
            .Select(line => line.Split('=', 2))
            .ToDictionary(kv => kv[0], kv => kv[1]);
        var state = (DTOs.DownloadStates.Http) downloader.Resolve(ini)!;

        Assert.Equal(Url, state.Url);
        Assert.Equal(new[] {"Referer:https://example.com/", "X-Token:abc:def"}, state.Headers);
    }

    [Theory]
    [InlineData("manualURL", "https://example.com/")]
    [InlineData("directURL", "not a url")]
    public void Resolve_WithoutAUsableDirectUrl_IsNotHttp(string key, string value)
    {
        var downloader = Downloader(new FakeHttp(_ => throw new InvalidOperationException()));

        Assert.Null(downloader.Resolve(new Dictionary<string, string> {{key, value}}));
    }

    [Theory]
    [InlineData("https://files.example.com/", true)]
    [InlineData("https://files.example.com/mods/", true)]
    [InlineData("https://other.example.com/", false)]
    public void IsAllowed_MatchesOnUrlPrefix(string prefix, bool allowed)
    {
        var downloader = Downloader(new FakeHttp(_ => throw new InvalidOperationException()));
        var allowList = new ServerAllowList {AllowedPrefixes = new[] {prefix}};

        Assert.Equal(allowed, downloader.IsAllowed(allowList, Archive().State));
    }

    [Fact]
    public async Task Download_SendsTheRecordedHeaders()
    {
        var data = Enumerable.Range(0, 5000).Select(i => (byte) i).ToArray();
        var http = new FakeHttp(request => FakeHttp.Ranged(request, data));
        var archive = Archive(data.Length, "Referer:https://example.com/page", "X-Token:abc:def");
        await using var dest = _manager.CreateFile();

        using var job = await new Resource<HttpDownloaderTests>("dl", 1).Begin("dl", data.Length, CancellationToken.None);
        await Downloader(http).Download(archive, (DTOs.DownloadStates.Http) archive.State, dest.Path, job,
            CancellationToken.None);

        var request = Assert.Single(http.Requests);
        Assert.Equal("https://example.com/page", request.Headers.Referrer!.ToString());
        Assert.Equal("abc:def", request.Headers.GetValues("X-Token").Single());
        Assert.Equal(data, await dest.Path.ReadAllBytesAsync());
    }

    [Fact]
    public async Task Verify_ArchiveOfTheRecordedSize_Passes()
    {
        var http = new FakeHttp(_ => FakeHttp.Ok(new byte[1234]));

        Assert.True(await Verify(http, Archive(1234)));
    }

    [Fact]
    public async Task Verify_ArchiveOfAnotherSize_Fails()
    {
        var http = new FakeHttp(_ => FakeHttp.Ok(new byte[1234]));

        Assert.False(await Verify(http, Archive(4321)));
    }

    [Fact]
    public async Task Verify_SizeNotRecorded_Passes()
    {
        var http = new FakeHttp(_ => FakeHttp.Ok(new byte[1234]));

        Assert.True(await Verify(http, Archive(0)));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Verify_ErrorStatus_Fails(HttpStatusCode status)
    {
        var http = new FakeHttp(_ => FakeHttp.Status(status, "", "application/octet-stream"));

        Assert.False(await Verify(http, Archive(1234)));
    }

    /// <summary>A host that has taken a file down often answers with a page rather than an error.</summary>
    [Theory]
    [InlineData("text/html")]
    [InlineData("text/plain")]
    [InlineData("application/xhtml+xml")]
    public async Task Verify_PageInsteadOfAFile_Fails(string mediaType)
    {
        var http = new FakeHttp(_ => FakeHttp.Ok(new byte[1234], mediaType));

        Assert.False(await Verify(http, Archive(1234)));
    }

    /// <summary>
    ///     With no length to compare, a redirect off to another site (a parking page, a login wall) is the
    ///     only sign the file has gone; a redirect within the same site is ordinary.
    /// </summary>
    [Theory]
    [InlineData("https://cdn.example.com/archive.7z", true)]
    [InlineData("https://parked-domains.invalid/", false)]
    public async Task Verify_NoLength_JudgesARedirectBySite(string landedOn, bool expected)
    {
        var http = new FakeHttp(request =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new MemoryStream(new byte[10])),
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, landedOn)
            };
            response.Content.Headers.ContentLength = null;
            return response;
        });

        Assert.Equal(expected, await Verify(http, Archive(1234)));
    }

    [Fact]
    public async Task ChunkedStream_ReadsByRange()
    {
        var data = Enumerable.Range(0, 3 << 20).Select(i => (byte) (i % 253)).ToArray();
        var http = new FakeHttp(request =>
        {
            var range = request.Headers.Range!.Ranges.Single();
            var to = (int) Math.Min(range.To!.Value, data.Length - 1);
            return FakeHttp.Ok(data[(int) range.From!.Value..(to + 1)]);
        });
        var archive = Archive(data.Length);

        await using var stream = await Downloader(http).GetChunkedSeekableStream(archive, CancellationToken.None);
        stream.Position = (2 << 20) + 100;
        var buffer = new byte[1000];
        var read = await stream.ReadAsync(buffer);

        Assert.Equal(1000, read);
        Assert.Equal(data.AsSpan((2 << 20) + 100, 1000).ToArray(), buffer);
        Assert.All(http.Requests, r => Assert.NotNull(r.Headers.Range));
    }
}
