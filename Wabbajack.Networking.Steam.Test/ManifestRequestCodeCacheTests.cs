using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Wabbajack.Networking.Steam.Test;

/// <summary>
///     Manifest request codes are mandatory and short lived, and Valve publishes no lifetime, so the cache
///     works to an estimate. What is testable is that the estimate is actually applied, that a code is not
///     shared between things that do not share one, and that a refused code is thrown away.
/// </summary>
public class ManifestRequestCodeCacheTests
{
    [Fact]
    public void ACodeIsReusedWhileItIsFresh()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var cache = new ManifestRequestCodeCache(TimeSpan.FromMinutes(5), () => now);

        cache.Set(489831, 3660787314279169352, "public", 1234);
        now = now.AddMinutes(4);

        Assert.Equal(1234ul, cache.Get(489831, 3660787314279169352, "public"));
    }

    [Fact]
    public void ACodeIsDroppedOnceItIsPastTheEstimate()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var cache = new ManifestRequestCodeCache(TimeSpan.FromMinutes(5), () => now);

        cache.Set(489831, 3660787314279169352, "public", 1234);
        now = now.AddMinutes(5);

        Assert.Null(cache.Get(489831, 3660787314279169352, "public"));
    }

    [Fact]
    public void NothingIsCachedUntilSomethingIsStored()
    {
        Assert.Null(new ManifestRequestCodeCache().Get(489831, 1, "public"));
    }

    [Theory]
    // A code is issued for one depot, one manifest and one branch. Handing it to any other combination
    // would be replaying a credential that was never meant for it.
    [InlineData(489832ul, 3660787314279169352ul, "public")]
    [InlineData(489831ul, 1ul, "public")]
    [InlineData(489831ul, 3660787314279169352ul, "beta")]
    public void ACodeDoesNotLeakToAnythingItWasNotIssuedFor(ulong depotId, ulong manifestId, string branch)
    {
        var cache = new ManifestRequestCodeCache();
        cache.Set(489831, 3660787314279169352, "public", 1234);

        Assert.Null(cache.Get((uint) depotId, manifestId, branch));
    }

    [Fact]
    public void ARefusedCodeIsForgotten()
    {
        var cache = new ManifestRequestCodeCache();
        cache.Set(489831, 3660787314279169352, "public", 1234);

        cache.Forget(489831, 3660787314279169352, "public");

        Assert.Null(cache.Get(489831, 3660787314279169352, "public"));
    }

    [Fact]
    public void ForgettingSomethingThatWasNeverCachedIsFine()
    {
        var cache = new ManifestRequestCodeCache();
        cache.Forget(489831, 3660787314279169352, "public");
    }
}
