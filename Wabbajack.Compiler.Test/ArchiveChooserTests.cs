using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;

namespace Wabbajack.Compiler.Test;

public class ArchiveChooserTests
{
    private static int[] Choose(int[][] candidates, long[]? sizes = null, int[]? alreadyNeeded = null)
    {
        var archives = candidates.SelectMany(c => c).Concat(alreadyNeeded ?? Array.Empty<int>()).Max() + 1;
        return ArchiveChooser.Choose(candidates, sizes ?? Enumerable.Repeat(1L, archives).ToArray(),
            (alreadyNeeded ?? Array.Empty<int>()).ToHashSet());
    }

    private static int ArchivesUsed(int[] chosen) => chosen.Distinct().Count();

    [Fact]
    public void AFileWithOneSourceUsesIt()
    {
        Assert.Equal(new[] {2}, Choose(new[] {new[] {2}}));
    }

    [Fact]
    public void PrefersAnArchiveTheListNeedsAnyway()
    {
        // The two Mod Organizer archives from the Horizon recompile: shared files could come from either,
        // but only 2.5.2 (archive 1) has the rest, so it is needed whatever happens.
        var candidates = new[]
        {
            new[] {0, 1}, new[] {0, 1}, new[] {0, 1},
            new[] {1}
        };

        Assert.Equal(new[] {1, 1, 1, 1}, Choose(candidates));
    }

    [Fact]
    public void UsesArchivesSomethingElseNeeds()
    {
        Assert.Equal(new[] {1}, Choose(new[] {new[] {0, 1}}, alreadyNeeded: new[] {1}));
    }

    [Fact]
    public void ARunningChoiceThatLooksRightCanBeBeatenByLaterFiles()
    {
        // Taking the files one at a time and keeping the first archive that could supply each would use 0,
        // then 1 and 2 as well. Archives 1 and 2 cover everything between them.
        var candidates = new[]
        {
            new[] {0, 1}, new[] {0, 2},
            new[] {1}, new[] {1}, new[] {2}, new[] {2}
        };

        var chosen = Choose(candidates);

        Assert.Equal(2, ArchivesUsed(chosen));
        Assert.DoesNotContain(0, chosen);
    }

    [Fact]
    public void CoversWithTheFewestArchivesItCanFind()
    {
        // No archive is forced. Archive 3 covers four of the six files, 4 the other two.
        var candidates = new[]
        {
            new[] {0, 3}, new[] {1, 3}, new[] {2, 3}, new[] {0, 3},
            new[] {1, 4}, new[] {2, 4}
        };

        var chosen = Choose(candidates);

        Assert.Equal(2, ArchivesUsed(chosen));
        Assert.Equal(new[] {3, 3, 3, 3, 4, 4}, chosen);
    }

    [Fact]
    public void BreaksATieWithTheSmallerDownload()
    {
        Assert.Equal(new[] {1, 1}, Choose(new[] {new[] {0, 1}, new[] {0, 1}}, sizes: new long[] {500, 100}));
    }

    [Fact]
    public void KeepsThePreferredCandidateAmongNeededArchives()
    {
        // Both archives are needed anyway, so the file takes the one listed first.
        Assert.Equal(new[] {0, 0, 1}, Choose(new[] {new[] {0, 1}, new[] {0}, new[] {1}}));
    }

    [Fact]
    public void ScalesToALargeList()
    {
        // 500,000 files over 5,000 archives, each file in one to three of them.
        var random = new Random(1234);
        var candidates = Enumerable.Range(0, 500_000)
            .Select(_ => Enumerable.Range(0, random.Next(1, 4)).Select(_ => random.Next(5_000)).Distinct().ToArray())
            .ToArray();

        var timer = Stopwatch.StartNew();
        var chosen = Choose(candidates);

        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(5), $"took {timer.Elapsed}");
        for (var i = 0; i < candidates.Length; i++)
            Assert.Contains(chosen[i], candidates[i]);
    }
}
