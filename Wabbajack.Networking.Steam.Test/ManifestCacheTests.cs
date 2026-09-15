using System;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Networking.Steam;
using Xunit;

namespace Wabbajack.Networking.Steam.Test;

/// <summary>
///     The bound and the eviction order. What is cached here is tens of megabytes per entry under a process
///     that may run all day, so the interesting property is not that it remembers but that it forgets, and
///     that it forgets the right one.
/// </summary>
public class ManifestCacheTests
{
    private const string Branch = "public";

    [Fact]
    public void WhatWasPutInComesBackOut()
    {
        var cache = new ManifestCache<string>(2);
        cache.Set(1, 100, Branch, "files");

        Assert.Equal("files", cache.Get(1, 100, Branch));
        Assert.Null(cache.Get(1, 101, Branch));
        Assert.Null(cache.Get(2, 100, Branch));
        Assert.Null(cache.Get(1, 100, "beta"));
    }

    [Fact]
    public void ItNeverHoldsMoreThanItsCapacity()
    {
        var cache = new ManifestCache<string>(3);

        for (uint depot = 1; depot <= 50; depot++)
            cache.Set(depot, depot, Branch, $"files {depot}");

        Assert.Equal(3, cache.Count);
        Assert.Equal(new[] {"files 48", "files 49", "files 50"},
            Enumerable.Range(48, 3).Select(d => cache.Get((uint) d, (ulong) d, Branch)).ToArray());
    }

    /// <summary>
    ///     The access pattern is a search: several manifests are read to find which one carries a file, then
    ///     the one that answered is read again to fetch it. Oldest-first would evict exactly that one.
    /// </summary>
    [Fact]
    public void ReadingAnEntryKeepsItAheadOfNewerOnes()
    {
        var cache = new ManifestCache<string>(2);
        cache.Set(1, 100, Branch, "first");
        cache.Set(2, 200, Branch, "second");

        Assert.Equal("first", cache.Get(1, 100, Branch));

        cache.Set(3, 300, Branch, "third");

        Assert.Equal("first", cache.Get(1, 100, Branch));
        Assert.Equal("third", cache.Get(3, 300, Branch));
        Assert.Null(cache.Get(2, 200, Branch));
    }

    /// <summary>
    ///     Two callers racing on one manifest read the same immutable build, so the first answer stored is
    ///     the one everybody gets. Handing the second caller a different object for the same thing is the
    ///     aliasing this exists to avoid.
    /// </summary>
    [Fact]
    public void SettingAKeyTwiceKeepsTheFirstValue()
    {
        var cache = new ManifestCache<string>(2);
        cache.Set(1, 100, Branch, "first");
        cache.Set(1, 100, Branch, "second");

        Assert.Equal("first", cache.Get(1, 100, Branch));
        Assert.Equal(1, cache.Count);
    }

    /// <summary>And a re-set counts as a use, or a duplicate write would quietly age the entry out.</summary>
    [Fact]
    public void SettingAHeldKeyCountsAsAUse()
    {
        var cache = new ManifestCache<string>(2);
        cache.Set(1, 100, Branch, "first");
        cache.Set(2, 200, Branch, "second");
        cache.Set(1, 100, Branch, "first again");
        cache.Set(3, 300, Branch, "third");

        Assert.Equal("first", cache.Get(1, 100, Branch));
        Assert.Null(cache.Get(2, 200, Branch));
    }

    [Fact]
    public void ACacheOfNothingIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ManifestCache<string>(0));
    }

    /// <summary>
    ///     Hammered from many threads at once, because everything it guards is reached from a repair running
    ///     several depot searches at a time.
    /// </summary>
    [Fact]
    public async Task ItSurvivesConcurrentUse()
    {
        var cache = new ManifestCache<string>(4);

        await Task.WhenAll(Enumerable.Range(0, 16).Select(worker => Task.Run(() =>
        {
            for (var i = 0; i < 5000; i++)
            {
                var depot = (uint) (i % 8);
                cache.Set(depot, depot, Branch, $"files {depot}");
                var held = cache.Get(depot, depot, Branch);
                if (held != null) Assert.Equal($"files {depot}", held);
            }
        })));

        Assert.True(cache.Count <= 4);
    }
}
