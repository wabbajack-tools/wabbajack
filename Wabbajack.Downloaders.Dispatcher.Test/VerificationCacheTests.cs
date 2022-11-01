using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Downloaders.Dispatcher.Test;


public class VerificationCacheTests
{
    private readonly ILogger<VerificationCache.VerificationCache> _logger;

    public VerificationCacheTests(ILogger<VerificationCache.VerificationCache> logger)
    {
        _logger = logger;
    }

    [Fact]
    public async Task BasicCacheTests()
    {
        using var cache = new VerificationCache.VerificationCache(_logger,  KnownFolders.EntryPoint.Combine(Guid.NewGuid().ToString()), TimeSpan.FromSeconds(1));

        var goodState = new DTOs.DownloadStates.Http { Url = new Uri($"https://some.com/{Guid.NewGuid()}/path") };
        var badState = new DTOs.DownloadStates.Http { Url = new Uri($"https://some.com/{Guid.NewGuid()}/path") };
        Assert.True(await cache.Get(goodState) == null);

        await cache.Put(goodState, true);
        Assert.True(await cache.Get(goodState));

        await Task.Delay(TimeSpan.FromSeconds(2));
        
        Assert.False(await cache.Get(goodState));

        await cache.Put(badState, true);
        Assert.True(await cache.Get(badState));
        await cache.Put(badState, false);
        Assert.Null(await cache.Get(badState));

    }
}

