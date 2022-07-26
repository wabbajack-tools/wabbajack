using System;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Downloaders;
using Wabbajack.DTOs;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.RateLimiter;
using Wabbajack.VFS;

namespace Wabbajack.Services.OSIntegrated.Services;

public class ModListDownloadMaintainer
{
    private readonly ILogger<ModListDownloadMaintainer> _logger;
    private readonly Configuration _configuration;
    private readonly DownloadDispatcher _dispatcher;
    private readonly FileHashCache _hashCache;
    private readonly IResource<DownloadDispatcher> _rateLimiter;
    private int _downloadingCount;
    private readonly DTOSerializer _dtos;

    public ModListDownloadMaintainer(ILogger<ModListDownloadMaintainer> logger, Configuration configuration,
        DownloadDispatcher dispatcher, FileHashCache hashCache, DTOSerializer dtos,  IResource<DownloadDispatcher> rateLimiter)
    {
        _logger = logger;
        _configuration = configuration;
        _dispatcher = dispatcher;
        _hashCache = hashCache;
        _rateLimiter = rateLimiter;
        _downloadingCount = 0;
        _dtos = dtos;
    }

    public AbsolutePath ModListPath(ModlistMetadata metadata)
    {
        return _configuration.ModListsDownloadLocation.Combine(metadata.NamespacedName.Replace("/", "_@@_")).WithExtension(Ext.Wabbajack);
    }

    public async Task<bool> HaveModList(ModlistMetadata metadata, CancellationToken? token = null)
    {
        token ??= CancellationToken.None;
        var path = ModListPath(metadata);
        if (!path.FileExists()) return false;

        if (_hashCache.TryGetHashCache(path, out var hash) && hash == metadata.DownloadMetadata!.Hash) return true;
        if (_downloadingCount > 0) return false;

        return await _hashCache.FileHashCachedAsync(path, token.Value) == metadata.DownloadMetadata!.Hash;
    }

    public (IObservable<Percent> Progress, Task Task) DownloadModlist(ModlistMetadata metadata, CancellationToken? token = null)
    {
        var path = ModListPath(metadata);
        
        token ??= CancellationToken.None;
        
        var progress = new Subject<Percent>();
        progress.OnNext(Percent.Zero);

        var tsk = Task.Run(async () =>
        {
            try
            {
                Interlocked.Increment(ref _downloadingCount);
                using var job = await _rateLimiter.Begin($"Downloading {metadata.Title}", metadata.DownloadMetadata!.Size,
                    token.Value);

                job.OnUpdate += (_, pr) => { progress.OnNext(pr.Progress); };

                var hash = await _dispatcher.Download(new Archive()
                {
                    State = _dispatcher.Parse(new Uri(metadata.Links.Download))!,
                    Size = metadata.DownloadMetadata.Size,
                    Hash = metadata.DownloadMetadata.Hash
                }, path, job, token.Value);

                _hashCache.FileHashWriteCache(path, hash);
                await path.WithExtension(Ext.MetaData).WriteAllTextAsync(JsonSerializer.Serialize(metadata));
            }
            finally
            {
                progress.OnCompleted();
                Interlocked.Decrement(ref _downloadingCount);
            }
        });

        return (progress, tsk);
    }
}