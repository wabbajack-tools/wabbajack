using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Http;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Xunit;

namespace Wabbajack.Downloaders.Dispatcher.Test.Offline;

/// <summary>
///     The downloader every Http and Nexus download goes through. It resumes from whatever is already on disk,
///     so what it leaves there after a failure decides what the next attempt produces.
/// </summary>
public class ResumableDownloaderTests : IAsyncLifetime
{
    private static readonly Uri Url = new("https://files.invalid/archive.7z");
    private static readonly byte[] Data = Enumerable.Range(0, 300_000).Select(i => (byte) (i * 31 % 251)).ToArray();

    private readonly TemporaryFileManager _manager;
    private TemporaryPath _folder;

    public ResumableDownloaderTests(TemporaryFileManager manager)
    {
        _manager = manager;
    }

    public Task InitializeAsync()
    {
        _folder = _manager.CreateFolder();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _folder.DisposeAsync();
    }

    private AbsolutePath Dest => _folder.Path.Combine("archive.7z");

    private static async Task<Hash> Download(FakeHttp http, AbsolutePath dest)
    {
        var downloader = new ResumableDownloader(NullLogger<ResumableDownloader>.Instance, http);
        using var job = await new Resource<ResumableDownloaderTests>("downloads", 1)
            .Begin("archive.7z", Data.Length, CancellationToken.None);
        return await downloader.Download(new HttpRequestMessage(HttpMethod.Get, Url), dest, job, CancellationToken.None);
    }

    [Fact]
    public async Task Downloads_AndHashesWhatItWrote()
    {
        var http = new FakeHttp(request => FakeHttp.Ranged(request, Data));

        var hash = await Download(http, Dest);

        Assert.Equal(Data, await Dest.ReadAllBytesAsync());
        Assert.Equal(await Data.Hash(), hash);
    }

    [Fact]
    public async Task PartialFileOnDisk_IsResumedFromItsEnd()
    {
        await Dest.WriteAllBytesAsync(Data[..100_000]);
        var http = new FakeHttp(request => FakeHttp.Ranged(request, Data));

        var hash = await Download(http, Dest);

        Assert.Equal(100_000, Assert.Single(http.Requests).Headers.Range!.Ranges.Single().From);
        Assert.Equal(Data, await Dest.ReadAllBytesAsync());
        Assert.Equal(await Data.Hash(), hash);
    }

    /// <summary>
    ///     The connection drops partway; the retry has to pick up where the bytes on disk actually end.
    /// </summary>
    [Fact]
    public async Task DroppedConnection_IsResumedToTheRightBytes()
    {
        var http = new FakeHttp((request, n) => FakeHttp.Ranged(request, Data, n == 0 ? 120_000 : null));

        var hash = await Download(http, Dest);

        Assert.Equal(Data, await Dest.ReadAllBytesAsync());
        Assert.Equal(await Data.Hash(), hash);
    }

    /// <summary>A server that ignores <c>Range</c> sends the whole file again, and it must not be appended.</summary>
    [Fact]
    public async Task ServerThatIgnoresTheRange_StartsOver()
    {
        await Dest.WriteAllBytesAsync(Data[..100_000]);
        var http = new FakeHttp(_ => FakeHttp.Ok(Data));

        var hash = await Download(http, Dest);

        Assert.Equal(Data, await Dest.ReadAllBytesAsync());
        Assert.Equal(await Data.Hash(), hash);
    }

    /// <summary>
    ///     A leftover file longer than the one being fetched (an older upload under the same name) has nothing
    ///     to resume; the server's 416 starts it over.
    /// </summary>
    [Fact]
    public async Task LeftoverLongerThanTheFile_StartsOver()
    {
        await Dest.WriteAllBytesAsync(new byte[Data.Length + 5000]);
        var http = new FakeHttp(request => FakeHttp.Ranged(request, Data));

        var hash = await Download(http, Dest);

        Assert.Equal(Data, await Dest.ReadAllBytesAsync());
        Assert.Equal(await Data.Hash(), hash);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ErrorPage_IsNotSavedAsTheArchive(HttpStatusCode status)
    {
        var http = new FakeHttp(_ => FakeHttp.Status(status, "<html>no such file</html>"));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Download(http, Dest));

        Assert.Equal(status, error.StatusCode);
        Assert.False(Dest.FileExists() && Dest.Size() > 0);
    }
}
