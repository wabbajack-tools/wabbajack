#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders.Http;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.Downloaders.VerificationCache;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Http.Interfaces;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Xunit;

namespace Wabbajack.Downloaders.Dispatcher.Test;

/// <summary>
///     The dispatcher used to swallow every <see cref="TaskCanceledException" /> and hand back a zero hash,
///     which callers read as a corrupt download. A stalled transfer is now a
///     <see cref="DownloadTimeoutException" /> and a real cancellation propagates.
/// </summary>
public class DownloadTimeoutTests
{
    private readonly IServiceProvider _provider;
    private readonly TemporaryFileManager _temp;

    public DownloadTimeoutTests(IServiceProvider provider, TemporaryFileManager temp)
    {
        _provider = provider;
        _temp = temp;
    }

    [Fact]
    public async Task TimeoutBecomesDownloadTimeoutException()
    {
        var archive = HttpArchive();
        var dispatcher = DispatcherWith(new ThrowingHttpDownloader(_ =>
            new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout")));

        await using var dest = _temp.CreateFile();
        var ex = await Assert.ThrowsAsync<DownloadTimeoutException>(() =>
            dispatcher.Download(archive, dest.Path, CancellationToken.None));

        Assert.Same(archive, ex.Archive);
        Assert.IsType<TaskCanceledException>(ex.InnerException);
    }

    [Fact]
    public async Task CancellationPropagatesAsOperationCanceled()
    {
        var archive = HttpArchive();
        using var cts = new CancellationTokenSource();
        var dispatcher = DispatcherWith(new ThrowingHttpDownloader(token =>
        {
            cts.Cancel();
            return new TaskCanceledException("cancelled by the user", null, token);
        }));

        await using var dest = _temp.CreateFile();
        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            dispatcher.Download(archive, dest.Path, cts.Token));

        Assert.IsNotType<DownloadTimeoutException>(ex);
    }

    [Fact]
    public async Task TimeoutOnPrimaryStillUsesTheMirror()
    {
        var bytes = Encoding.UTF8.GetBytes("served from the mirror");
        var archive = HttpArchive(await bytes.Hash(), bytes.Length);
        var mirrorUrl = _provider.GetRequiredService<Client>().GetMirrorUrl(archive.Hash);
        var dispatcher = DispatcherWith(
            new ThrowingHttpDownloader(_ => new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout")),
            new FakeMirrorDownloader(mirrorUrl, bytes));

        await using var dest = _temp.CreateFile();
        var (result, hash) = await dispatcher.DownloadWithPossibleUpgrade(archive, dest.Path, CancellationToken.None);

        Assert.Equal(DownloadResult.Mirror, result);
        Assert.Equal(archive.Hash, hash);
        Assert.Equal(bytes, await dest.Path.ReadAllBytesAsync());
    }

    [Fact]
    public async Task TimeoutWithNoMirrorRethrows()
    {
        var bytes = Encoding.UTF8.GetBytes("never served");
        var archive = HttpArchive(await bytes.Hash(), bytes.Length);
        var mirrorUrl = _provider.GetRequiredService<Client>().GetMirrorUrl(archive.Hash);
        var dispatcher = DispatcherWith(
            new ThrowingHttpDownloader(_ => new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout")),
            new FakeMirrorDownloader(mirrorUrl, null));

        await using var dest = _temp.CreateFile();
        var ex = await Assert.ThrowsAsync<DownloadTimeoutException>(() =>
            dispatcher.DownloadWithPossibleUpgrade(archive, dest.Path, CancellationToken.None));

        Assert.Same(archive, ex.Archive);
        Assert.IsType<TaskCanceledException>(ex.InnerException);
    }

    private static Archive HttpArchive(Hash hash = default, long size = 1024)
    {
        return new Archive
        {
            Name = "WABBAJACK_TEST_FILE.zip",
            Size = size,
            Hash = hash,
            State = new DTOs.DownloadStates.Http {Url = new Uri("https://example.com/WABBAJACK_TEST_FILE.zip")}
        };
    }

    private DownloadDispatcher DispatcherWith(IHttpDownloader httpDownloader, params IDownloader[] others)
    {
        var http = new HttpDownloader(_provider.GetRequiredService<ILogger<HttpDownloader>>(),
            _provider.GetRequiredService<HttpClient>(), httpDownloader);

        return new DownloadDispatcher(_provider.GetRequiredService<ILogger<DownloadDispatcher>>(),
            new IDownloader[] {http}.Concat(others),
            _provider.GetRequiredService<IResource<DownloadDispatcher>>(),
            _provider.GetRequiredService<Client>(),
            _provider.GetRequiredService<IVerificationCache>(),
            useProxyCache: false);
    }

    private class ThrowingHttpDownloader : IHttpDownloader
    {
        private readonly Func<CancellationToken, Exception> _failure;

        public ThrowingHttpDownloader(Func<CancellationToken, Exception> failure)
        {
            _failure = failure;
        }

        public Task<Hash> Download(HttpRequestMessage message, AbsolutePath dest, IJob job, CancellationToken token)
        {
            throw _failure(token);
        }
    }

    /// <summary>
    ///     Stands in for the CDN downloader the mirror fallback goes through. Serves <c>bytes</c> for the
    ///     expected mirror URL, or reports "not on the mirror" the way the real one does when given null.
    /// </summary>
    private class FakeMirrorDownloader : ADownloader<WabbajackCDN>
    {
        private readonly Uri _mirrorUrl;
        private readonly byte[]? _bytes;

        public FakeMirrorDownloader(Uri mirrorUrl, byte[]? bytes)
        {
            _mirrorUrl = mirrorUrl;
            _bytes = bytes;
        }

        public override Priority Priority => Priority.Normal;

        public override async Task<Hash> Download(Archive archive, WabbajackCDN state, AbsolutePath destination,
            IJob job, CancellationToken token)
        {
            Assert.Equal(_mirrorUrl, state.Url);
            if (_bytes == null)
                throw new NotSupportedException($"Nothing on the mirror for {state.Url}");
            await destination.WriteAllBytesAsync(_bytes, token);
            return await _bytes.Hash();
        }

        public override Task<bool> Prepare()
        {
            return Task.FromResult(true);
        }

        public override bool IsAllowed(ServerAllowList allowList, IDownloadState state)
        {
            return true;
        }

        public override IDownloadState? Resolve(IReadOnlyDictionary<string, string> iniData)
        {
            return null;
        }

        public override Task<bool> Verify(Archive archive, WabbajackCDN archiveState, IJob job, CancellationToken token)
        {
            return Task.FromResult(true);
        }

        public override IEnumerable<string> MetaIni(Archive a, WabbajackCDN state)
        {
            return Array.Empty<string>();
        }
    }
}
