using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders.VerificationCache;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths.IO;

namespace Wabbajack.Downloaders.Dispatcher.Test;


[ClassConstructor<DispatcherClassConstructor>]
public class VerificationCacheTests
{
    private readonly ILogger<VerificationCache.VerificationCache> _logger;
    private readonly DTOSerializer _dtos;

    public VerificationCacheTests(ILogger<VerificationCache.VerificationCache> logger, DTOSerializer dtos)
    {
        _logger = logger;
        _dtos = dtos;
    }

    [Test]
    public async Task BasicCacheTests()
    {
        using var cacheBase = new VerificationCache.VerificationCache(_logger,  
            KnownFolders.EntryPoint.Combine(Guid.NewGuid().ToString()), 
            TimeSpan.FromSeconds(1),
            _dtos);

        var cache = (IVerificationCache)cacheBase;

        var goodState = new DTOs.DownloadStates.Http { Url = new Uri($"https://some.com/{Guid.NewGuid()}/path") };
        var badState = new DTOs.DownloadStates.Http { Url = new Uri($"https://some.com/{Guid.NewGuid()}/path") };
        await Assert.That((await cache.Get(goodState)).IsValid == null).IsTrue();

        await cache.Put(goodState, true);
        var result = await cache.Get(goodState);
        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.State).IsTypeOf<DTOs.DownloadStates.Http>();

        await Task.Delay(TimeSpan.FromSeconds(2));

        await Assert.That((await cache.Get(goodState)).IsValid).IsFalse();

        await cache.Put(badState, true);
        await Assert.That((await cache.Get(badState)).IsValid).IsTrue();
        await cache.Put(badState, false);
        await Assert.That((await cache.Get(badState)).IsValid).IsNull();

    }
}

