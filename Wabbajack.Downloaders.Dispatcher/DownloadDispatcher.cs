using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders.Interfaces;
using Wabbajack.DTOs;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.DTOs.ServerResponses;
using Wabbajack.DTOs.Validation;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Networking.Http;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;

namespace Wabbajack.Downloaders;

public class DownloadDispatcher
{
    private readonly IDownloader[] _downloaders;
    private readonly IResource<DownloadDispatcher> _limiter;
    private readonly ILogger<DownloadDispatcher> _logger;
    private readonly Client _wjClient;
    private readonly bool _useProxyCache;

    public DownloadDispatcher(ILogger<DownloadDispatcher> logger, IEnumerable<IDownloader> downloaders,
        IResource<DownloadDispatcher> limiter, Client wjClient, bool useProxyCache = true)
    {
        _downloaders = downloaders.OrderBy(d => d.Priority).ToArray();
        _logger = logger;
        _wjClient = wjClient;
        _limiter = limiter;
        _useProxyCache = useProxyCache;
    }

    public async Task<Hash> Download(Archive a, AbsolutePath dest, CancellationToken token)
    {
        using var downloadScope = _logger.BeginScope("Downloading {Name}", a.Name);
        using var job = await _limiter.Begin("Downloading " + a.Name, a.Size, token);
        return await Download(a, dest, job, token);
    }

    public async Task<Hash> Download(Archive a, AbsolutePath dest, Job<DownloadDispatcher> job, CancellationToken token)
    {
        if (!dest.Parent.DirectoryExists())
            dest.Parent.CreateDirectory();

        var downloader = Downloader(a);
        if (_useProxyCache && downloader is IProxyable p)
        {
            var uri = p.UnParse(a.State);
            var newUri = _wjClient.MakeProxyUrl(a, uri);
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

        var hash = await downloader.Download(a, dest, job, token);
        return hash;
    }

    public async Task<IDownloadState?> ResolveArchive(IReadOnlyDictionary<string, string> ini)
    {
        return _downloaders.Select(downloader => downloader.Resolve(ini)).FirstOrDefault(result => result != null);
    }

    public async Task<bool> Verify(Archive a, CancellationToken token)
    {
        try
        {
            var downloader = Downloader(a);
            using var job = await _limiter.Begin($"Verifying {a.State.PrimaryKeyString}", -1, token);
            return await downloader.Verify(a, job, token);
        }
        catch (HttpException)
        {
            return false;
        }
    }

    public async Task<(DownloadResult, Hash)> DownloadWithPossibleUpgrade(Archive archive, AbsolutePath destination,
        CancellationToken token)
    {
        var downloadedHash = await Download(archive, destination, token);
        if (downloadedHash != default && (downloadedHash == archive.Hash || archive.Hash == default))
            return (DownloadResult.Success, downloadedHash);

        downloadedHash = await DownloadFromMirror(archive, destination, token);
        if (downloadedHash != default) return (DownloadResult.Mirror, downloadedHash);

        return (DownloadResult.Failure, downloadedHash);

        // TODO: implement patching
        /*
        if (!(archive.State is IUpgradingState))
        {
            _logger.LogInformation("Download failed for {name} and no upgrade from this download source is possible", archive.Name);
            return DownloadResult.Failure;
        }

        _logger.LogInformation("Trying to find solution to broken download for {name}", archive.Name);
        
        var result = await FindUpgrade(archive);
        if (result == default )
        {
            result = await AbstractDownloadState.ServerFindUpgrade(archive);
            if (result == default)
            {
                _logger.LogInformation(
                    "No solution for broken download {name} {primaryKeyString} could be found", archive.Name, archive.State.PrimaryKeyString);
                return DownloadResult.Failure;
            }
        }

        _logger.LogInformation($"Looking for patch for {archive.Name} ({(long)archive.Hash} {archive.Hash.ToHex()} -> {(long)result.Archive!.Hash} {result.Archive!.Hash.ToHex()})");
        var patchResult = await ClientAPI.GetModUpgrade(archive, result.Archive!);

        _logger.LogInformation($"Downloading patch for {archive.Name} from {patchResult}");
        
        var tempFile = new TempFile();

        if (WabbajackCDNDownloader.DomainRemaps.TryGetValue(patchResult.Host, out var remap))
        {
            var builder = new UriBuilder(patchResult) {Host = remap};
            patchResult = builder.Uri;
        }

        using var response = await (await ClientAPI.GetClient()).GetAsync(patchResult);

        await tempFile.Path.WriteAllAsync(await response.Content.ReadAsStreamAsync());
        response.Dispose();

        _logger.LogInformation($"Applying patch to {archive.Name}");
        await using(var src = await result.NewFile.Path.OpenShared())
        await using (var final = await destination.Create())
        {
            Utils.ApplyPatch(src, () => tempFile.Path.OpenShared().Result, final);
        }

        var hash = await destination.FileHashCachedAsync();
        if (hash != archive.Hash && archive.Hash != default)
        {
            _logger.LogInformation("Archive hash didn't match after patching");
            return DownloadResult.Failure;
        }

        return DownloadResult.Update;
        */
    }

    private async Task<Hash> DownloadFromMirror(Archive archive, AbsolutePath destination, CancellationToken token)
    {
        try
        {
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
        catch (Exception ex)
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

    public async Task<bool> IsAllowed(ModUpgradeRequest request, CancellationToken allowList)
    {
        throw new NotImplementedException();
    }

    public async Task<Archive?> FindUpgrade(Archive archive, TemporaryFileManager fileManager, CancellationToken token)
    {
        try
        {
            var downloader = Downloader(archive);
            if (downloader is not IUpgradingDownloader ud) return null;

            var job = await _limiter.Begin($"Finding upgrade for {archive.Name} - {archive.State.PrimaryKeyString}", 0,
                token);
            return await ud.TryGetUpgrade(archive, job, fileManager, token);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "While finding upgrade for {PrimaryKeyString}", archive.State.PrimaryKeyString);
            return null;
        }
    }

    public async Task PrepareAll(IEnumerable<IDownloadState> downloadStates)
    {
        foreach (var d in downloadStates.Select(d => Downloader(new Archive {State = d})).Distinct())
            await d.Prepare();
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