using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Wabbajack.Paths.IO.Test;

public class RandomNameTests
{
    [Fact]
    public void Next_UsesRequestedLength()
    {
        Assert.Equal(RandomName.DefaultLength, RandomName.Next().Length);
        Assert.Equal(8, RandomName.Next(8).Length);
    }

    [Fact]
    public void Next_ProducesOnlyPathSafeCharacters()
    {
        foreach (var c in string.Concat(Enumerable.Range(0, 200).Select(_ => RandomName.Next())))
            Assert.True(char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c), $"unexpected character '{c}'");
    }

    /// <summary>
    ///     Every temp file and folder in the app is named from this generator, and the pipelines that call it
    ///     run in parallel. A generator that repeats under concurrency hands two callers the same path, so they
    ///     overwrite each other's data.
    ///
    ///     The count is high on purpose. The generator this replaced repeated roughly 2.4 times per 200 names,
    ///     which a smaller sample detects only intermittently.
    /// </summary>
    [Fact]
    public void Next_ProducesUniqueNamesUnderConcurrency()
    {
        const int count = 50_000;

        var names = new ConcurrentBag<string>();
        Parallel.For(0, count, _ => names.Add(RandomName.Next()));

        Assert.Equal(count, names.Count);
        Assert.Equal(count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
