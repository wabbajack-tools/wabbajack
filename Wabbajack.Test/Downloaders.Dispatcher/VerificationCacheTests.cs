using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders.VerificationCache;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Downloaders.Dispatcher.Test;


public class VerificationCacheTests
{
    private readonly ILogger<VerificationCache.VerificationCache> _logger;
    private readonly DTOSerializer _dtos;

    public VerificationCacheTests(ILogger<VerificationCache.VerificationCache> logger, DTOSerializer dtos)
    {
        _logger = logger;
        _dtos = dtos;
    }

    [Fact]
    public async Task BasicCacheTests()
    {
        using var cacheBase = new VerificationCache.VerificationCache(_logger,  
            KnownFolders.EntryPoint.Combine(Guid.NewGuid().ToString()), 
            TimeSpan.FromSeconds(1),
            _dtos);

        var cache = (IVerificationCache)cacheBase;

        var goodState = new DTOs.DownloadStates.Http { Url = new Uri($"https://some.com/{Guid.NewGuid()}/path") };
        var badState = new DTOs.DownloadStates.Http { Url = new Uri($"https://some.com/{Guid.NewGuid()}/path") };
        Assert.True((await cache.Get(goodState)).IsValid == null);

        await cache.Put(goodState, true);
        var result = await cache.Get(goodState);
        Assert.True(result.IsValid);
        Assert.IsType<DTOs.DownloadStates.Http>(result.State);

        await Task.Delay(TimeSpan.FromSeconds(2));
        
        Assert.False((await cache.Get(goodState)).IsValid);

        await cache.Put(badState, true);
        Assert.True((await cache.Get(badState)).IsValid);
        await cache.Put(badState, false);
        Assert.Null((await cache.Get(badState)).IsValid);

    }
}

