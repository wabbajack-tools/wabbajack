using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.Downloaders.VerificationCache;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Http;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using StringExtensions = Wabbajack.Paths.StringExtensions;

namespace Wabbajack.Downloaders;

public class DownloadDispatcher
{
    private readonly IDownloader[] _downloaders;
    private readonly IResource<DownloadDispatcher> _limiter;
    private readonly ILogger<DownloadDispatcher> _logger;
    private readonly Client _wjClient;
    private readonly bool _useProxyCache;
    private readonly IVerificationCache _verificationCache;

    public DownloadDispatcher(ILogger<DownloadDispatcher> logger, IEnumerable<IDownloader> downloaders,
        IResource<DownloadDispatcher> limiter, Client wjClient, IVerificationCache verificationCache, bool useProxyCache = true)
    {
        _downloaders = downloaders.OrderBy(d => d.Priority).ToArray();
        _logger = logger;
        _wjClient = wjClient;
        _limiter = limiter;
        _useProxyCache = useProxyCache;
        _verificationCache = verificationCache;
    }

    public bool UseProxy { get; set; } = false;

    public async Task<Hash> Download(Archive a, AbsolutePath dest, CancellationToken token, bool? proxy = null)
    {
        if (token.IsCancellationRequested)
        {
            return new Hash();
        }

        using var downloadScope = _logger.BeginScope("Downloading {Name}", a.Name);
        using var job = await _limiter.Begin("Downloading " + a.Name, a.Size, token);
        var hash = await Download(a, dest, job, token, proxy);
        _logger.LogInformation("Finished downloading {name}. Hash: {hash}; Size: {size}/{expectedSize}", a.Name, hash, dest.Size().ToFileSizeString(), a.Size.ToFileSizeString());
        return hash;
    }

    public async Task<Archive> MaybeProxy(Archive a, CancellationToken token)
    {
        if (!UseProxy) return a;
        var downloader = Downloader(a);
        if (downloader is not IProxyable p) return a;
        
        var uri = p.UnParse(a.State);
        var newUri = await _wjClient.MakeProxyUrl(a, uri);
        if (newUri != null)
        {
            a = new Archive
            {
                Name = a.Name,
                Size = a.Size,
                Hash = a.Hash,
                State = new DTOs.DownloadStates.Http()
                {
                    Url = newUri
                }
            };
        }

        return a;
    }

    public async Task<Hash> Download(Archive a, AbsolutePath dest, Job<DownloadDispatcher> job, CancellationToken token, bool? useProxy = null)
    {
        var requested = a;
        try
        {
            if (!dest.Parent.DirectoryExists())
                dest.Parent.CreateDirectory();

            var downloader = Downloader(a);
            if ((useProxy ?? _useProxyCache) && downloader is IProxyable p)
            {
                var uri = p.UnParse(a.State);
                var newUri = await _wjClient.MakeProxyUrl(a, uri);
                if (newUri != null)
                {
                    a = new Archive
                    {
                        Name = a.Name,
                        Size = a.Size,
                        Hash = a.Hash,
                        State = new DTOs.DownloadStates.Http()
                        {
                            Url = newUri
                        }
                    };
                    downloader = Downloader(a);
                    _logger.LogInformation("Downloading Proxy ({Hash}) {Uri}", (await uri.ToString().Hash()).ToHex(), uri);
                }
            }

            var hash = await downloader.Download(a, dest, job, token);
            return hash;
        }
        catch (TaskCanceledException ex) when (!token.IsCancellationRequested)
        {
            // HttpClient reports a stalled transfer as a cancellation. The user did not cancel, so
            // surface it as something a caller can retry rather than as a zero hash.
            throw new DownloadTimeoutException(requested, ex);
        }
    }

    public Task<IDownloadState?> ResolveArchive(IReadOnlyDictionary<string, string> ini)
    {
        return Task.FromResult(_downloaders.Select(downloader => downloader.Resolve(ini)).FirstOrDefault(result => result != null));
    }

    public async Task<bool> Verify(Archive a, CancellationToken token)
    {
        try
        {
            var (valid, newState) = await _verificationCache.Get(a.State);
            if (valid == true && newState is not null)
            {
                a.State = newState;
                return true;
            }

            if (UseProxy) 
                a = await MaybeProxy(a, token);
            
            var downloader = Downloader(a);
            using var job = await _limiter.Begin($"Verifying {a.State.PrimaryKeyString}", -1, token);
            var result = await downloader.Verify(a, job, token);
            await _verificationCache.Put(a.State, result);

            return result;
        }
        catch (HttpException ex)
        {
            _logger.LogError($"Failed verifying {a.State.PrimaryKeyString}: {ex}");
            await _verificationCache.Put(a.State, false);
            return false;
        }
    }

    public async Task<(DownloadResult, Hash)> DownloadWithPossibleUpgrade(Archive archive, AbsolutePath destination,
        CancellationToken token)
    {
        Hash downloadedHash;
        try
        {
            downloadedHash = await Download(archive, destination, token);
        }
        catch (Exception ex) when (ex is ManualDownloadRequiredException or DownloadTimeoutException)
        {
            // The source needs a browser or the transfer stalled, but the mirror may still have the file.
            _logger.LogInformation("{archive} could not be downloaded from its source ({reason}), trying mirror first",
                archive.Name, ex.Message);
            Hash mirrorHash = default;
            try
            {
                mirrorHash = await DownloadFromMirror(archive, destination, token);
            }
            catch (NotSupportedException)
            {
                _logger.LogInformation("Could not find archive {archive} on mirror", archive.Name);
            }

            if (mirrorHash != default) return (DownloadResult.Mirror, mirrorHash);
            throw;
        }

        if (downloadedHash != default && (downloadedHash == archive.Hash || archive.Hash == default))
            return (DownloadResult.Success, downloadedHash);

        try
        {
            _logger.LogWarning("Initial download of {archive} failed, trying mirror", archive.Name);
            downloadedHash = await DownloadFromMirror(archive, destination, token);
            if (downloadedHash != default) return (DownloadResult.Mirror, downloadedHash);
        }
        catch (NotSupportedException)
        {
            _logger.LogInformation("Could not find archive {archive} on mirror", archive.Name);
            // Thrown if downloading from mirror is not supported for archive, keep original hash
        }

        return (DownloadResult.Failure, downloadedHash);
    }
    
    private async Task<Hash> DownloadFromMirror(Archive archive, AbsolutePath destination, CancellationToken token)
    {
        try
        {
            _logger.LogInformation("Downloading {archiveName} from mirror, hash {archiveHash}", archive.Name, archive.Hash);
            var url = _wjClient.GetMirrorUrl(archive.Hash);
            if (url == null) return default;

            var newArchive =
                new Archive
                {
                    Hash = archive.Hash,
                    Size = archive.Size,
                    Name = archive.Name,
                    State = new WabbajackCDN {Url = url}
                };

            return await Download(newArchive, destination, token);
        }
        catch (Exception ex) when (ex is not NotSupportedException)
        {
            _logger.LogCritical(ex, "While finding mirror for {hash}", archive.Hash);
            return default;
        }
    }

    public IDownloader Downloader(Archive archive)
    {
        var result = _downloaders.FirstOrDefault(d => d.CanDownload(archive));
        if (result != null) return result!;
        _logger.LogError("No downloader found for {type}", archive.State.GetType());
        throw new NotImplementedException($"No downloader for {archive.State.GetType()}");
    }

    public bool TryGetDownloader(Archive archive, out IDownloader downloader)
    {
        var result = _downloaders.FirstOrDefault(d => d.CanDownload(archive));
        if (result != null)
        {
            downloader = result!;
            return true;
        }

        downloader = _downloaders.First();
        return false;
    }

    public async Task<Archive> FillInMetadata(Archive a)
    {
        var downloader = Downloader(a);
        if (downloader is IMetaStateDownloader msd)
            return await msd.FillInMetadata(a);
        return a;
    }

    public IDownloadState? Parse(Uri url)
    {
        return _downloaders.OfType<IUrlDownloader>()
            .Select(downloader => downloader.Parse(url))
            .FirstOrDefault(parsed => parsed != null);
    }

    public IEnumerable<string> MetaIni(Archive archive)
    {
        return Downloader(archive).MetaIni(archive);
    }

    public string MetaIniSection(Archive archive)
    {
        return string.Join("\n", new[] {"[General]"}.Concat(MetaIni(archive)));
    }

    public bool IsAllowed(Archive archive, ServerAllowList allowList)
    {
        return Downloader(archive).IsAllowed(allowList, archive.State);
    }

    public Task<IEnumerable<IDownloader>> AllDownloaders(IEnumerable<IDownloadState> downloadStates)
    {
        return Task.FromResult(downloadStates.Select(d => Downloader(new Archive {State = d})).Distinct());
    }

    public bool Matches(Archive archive, ServerAllowList mirrorAllowList)
    {
        if (archive.State is DTOs.DownloadStates.GoogleDrive gdrive)
            return mirrorAllowList.GoogleIDs?.Contains(gdrive.Id) ?? false;

        var downloader = Downloader(archive);

        if (downloader is not IUrlDownloader ud) return false;
        var url = ud.UnParse(archive.State).ToString();
        return mirrorAllowList.AllowedPrefixes.Any(p => url.StartsWith(p));
    }

    public async ValueTask<Stream> ChunkedSeekableStream(Archive archive, CancellationToken token)
    {
        if (!TryGetDownloader(archive, out var downloader))
        {
            throw new NotImplementedException($"Now downloader ot handle {archive.State}");
        }
        
        
        if (downloader is IChunkedSeekableStreamDownloader cs)
        {
            return await cs.GetChunkedSeekableStream(archive, token);
        }
        else
        {
            throw new NotImplementedException($"Downloader {archive.State} does not support chunked seekable streams");
        }
    }
}