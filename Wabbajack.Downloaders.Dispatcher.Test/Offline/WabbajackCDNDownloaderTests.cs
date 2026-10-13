#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Wabbajack.DTOs;
using Wabbajack.DTOs.CDN;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Xunit;

namespace Wabbajack.Downloaders.Dispatcher.Test.Offline;

/// <summary>
///     The CDN serves a file as a gzipped definition and numbered parts, each with its own hash. These serve
///     one from <see cref="FakeHttp" />: parts of 2 MB, the size the CDN cuts them at and the size its seekable
///     stream assumes.
/// </summary>
public class WabbajackCDNDownloaderTests : IAsyncLifetime
{
    private const int PartSize = 1 << 21;
    private static readonly Uri Url = new("https://wabbajack.b-cdn.net/archive.7z_0123");

    private readonly DTOSerializer _dtos;
    private readonly TemporaryFileManager _manager;
    private TemporaryPath _folder;

    public WabbajackCDNDownloaderTests(DTOSerializer dtos, TemporaryFileManager manager)
    {
        _dtos = dtos;
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

    private static readonly byte[] Data =
        Enumerable.Range(0, PartSize * 2 + 12_345).Select(i => (byte) (i * 7 % 255)).ToArray();

    private static async Task<FileDefinition> Definition(byte[] data)
    {
        var parts = new List<PartDefinition>();
        for (var offset = 0; offset < data.Length; offset += PartSize)
        {
            var part = data.AsSpan(offset, Math.Min(PartSize, data.Length - offset)).ToArray();
            parts.Add(new PartDefinition {Index = parts.Count, Offset = offset, Size = part.Length, Hash = await part.Hash()});
        }

        return new FileDefinition
        {
            OriginalFileName = "archive.7z".ToRelativePath(),
            ServerAssignedUniqueId = "0123",
            Size = data.Length,
            Hash = await data.Hash(),
            Parts = parts.ToArray()
        };
    }

    /// <summary>Serves <paramref name="data" />; <paramref name="spoil" /> can replace a part's bytes.</summary>
    private async Task<FakeHttp> Cdn(byte[] data, Func<long, int, byte[], byte[]>? spoil = null)
    {
        var definition = await Definition(data);
        using var gz = new MemoryStream();
        await using (var zip = new GZipStream(gz, CompressionLevel.Fastest, true))
            await _dtos.Serialize(definition, zip);
        var gzipped = gz.ToArray();

        return new FakeHttp((request, n) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/definition.json.gz"))
                return FakeHttp.Ok(gzipped);
            var index = long.Parse(path[(path.LastIndexOf('/') + 1)..]);
            var part = definition.Parts[index];
            var bytes = data.AsSpan((int) part.Offset, (int) part.Size).ToArray();
            return FakeHttp.Ok(spoil?.Invoke(index, n, bytes) ?? bytes);
        });
    }

    private WabbajackCDNDownloader Downloader(FakeHttp http)
    {
        return new WabbajackCDNDownloader(NullLogger<WabbajackCDNDownloader>.Instance, http.Client(),
            new Resource<HttpClient>("cdn", 4), _dtos);
    }

    private static Archive Archive() => new() {Name = "archive.7z", Size = Data.Length, State = new WabbajackCDN {Url = Url}};

    private async Task<Hash> Download(FakeHttp http, AbsolutePath dest)
    {
        using var job = await new Resource<WabbajackCDNDownloaderTests>("dl", 1).Begin("dl", Data.Length, CancellationToken.None);
        var archive = Archive();
        return await Downloader(http).Download(archive, (WabbajackCDN) archive.State, dest, job, CancellationToken.None);
    }

    [Theory]
    [InlineData("https://wabbajack.b-cdn.net/x", true)]
    [InlineData("https://authored-files.wabbajack.org/x", true)]
    [InlineData("https://mirror.wabbajack.org/x", true)]
    [InlineData("https://patches.wabbajack.org/x", true)]
    [InlineData("https://example.com/x", false)]
    public void Parse_TakesOnlyCdnHosts(string url, bool isCdn)
    {
        var downloader = Downloader(new FakeHttp(_ => throw new InvalidOperationException()));

        Assert.Equal(isCdn, downloader.Parse(new Uri(url)) is WabbajackCDN);
    }

    [Fact]
    public async Task Download_AssemblesThePartsInOrder()
    {
        var http = await Cdn(Data);
        var dest = _folder.Path.Combine("archive.7z");

        var hash = await Download(http, dest);

        Assert.Equal(await Data.Hash(), hash);
        Assert.Equal(Data, await dest.ReadAllBytesAsync());
    }

    /// <summary>The old bunny CDN host names still appear in lists, and are fetched from their new hosts.</summary>
    [Fact]
    public async Task Download_FromAnOldHostName_GoesToItsNewHost()
    {
        var http = await Cdn(Data);

        await Download(http, _folder.Path.Combine("archive.7z"));

        Assert.All(http.Requests, r =>
        {
            Assert.Equal("authored-files.wabbajack.org", r.RequestUri!.Host);
            Assert.Equal("authored-files.wabbajack.org", r.Headers.Host);
        });
    }

    [Fact]
    public async Task Download_PartThatArrivesDamagedOnce_IsFetchedAgain()
    {
        var http = await Cdn(Data, (index, n, bytes) => index == 1 && n < 3 ? bytes.Reverse().ToArray() : bytes);
        var dest = _folder.Path.Combine("archive.7z");

        var hash = await Download(http, dest);

        Assert.Equal(await Data.Hash(), hash);
        Assert.Equal(Data, await dest.ReadAllBytesAsync());
    }

    [Fact]
    public async Task Download_PartThatIsAlwaysDamaged_Fails()
    {
        var http = await Cdn(Data, (index, _, bytes) => index == 2 ? new byte[bytes.Length] : bytes);

        var error = await Assert.ThrowsAnyAsync<Exception>(() => Download(http, _folder.Path.Combine("archive.7z")));

        Assert.Contains("Invalid part hash 2", error.Message);
    }

    [Fact]
    public async Task Verify_FollowsTheDefinition()
    {
        var present = await Cdn(Data);
        var gone = new FakeHttp(_ => FakeHttp.Status(HttpStatusCode.NotFound));
        using var job = await new Resource<WabbajackCDNDownloaderTests>("v", 1).Begin("v", 0, CancellationToken.None);
        var archive = Archive();

        Assert.True(await Downloader(present).Verify(archive, (WabbajackCDN) archive.State, job, CancellationToken.None));
        Assert.False(await Downloader(gone).Verify(archive, (WabbajackCDN) archive.State, job, CancellationToken.None));
    }

    [Fact]
    public async Task ChunkedStream_ReadsAcrossAPartBoundary()
    {
        var http = await Cdn(Data);

        await using var stream = await Downloader(http).GetChunkedSeekableStream(Archive(), CancellationToken.None);
        stream.Position = PartSize - 500;
        var buffer = new byte[1000];
        var read = 0;
        while (read < buffer.Length)
            read += await stream.ReadAsync(buffer.AsMemory(read));

        Assert.Equal(Data.AsSpan(PartSize - 500, 1000).ToArray(), buffer);
    }
}
