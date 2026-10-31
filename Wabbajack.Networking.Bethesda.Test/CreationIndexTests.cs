using System;
using System.IO;
using System.Linq;
using Wabbajack.Paths;
using Xunit;

namespace Wabbajack.Networking.Bethesda.Test;

/// <summary>
///     The committed table and the one question asked of it. The integrity half is worth pinning because
///     the table is generated: a regeneration that drops rows, pairs a plugin with the wrong id, or lands
///     two Creations on the same stem produces a table that still parses and then answers wrongly, which
///     surfaces as a Creation that cannot be fetched rather than as anything that looks like a bug here.
/// </summary>
public class CreationIndexTests
{
    private static readonly CreationIndex Index = CreationIndex.Default;

    [Fact]
    public void CarriesEveryAnniversaryEditionCreation()
    {
        Assert.Equal(CreationIndex.ExpectedCount, Index.Creations.Count);
    }

    [Fact]
    public void EveryContentIdIsUnique()
    {
        Assert.Equal(Index.Creations.Count, Index.Creations.Select(c => c.ContentId).Distinct().Count());
    }

    /// <summary>
    ///     Stems, not plugin names: a plugin name that is unique is not enough, because a duplicated stem
    ///     would make the archive of one Creation resolve to another.
    /// </summary>
    [Fact]
    public void EveryPluginStemIsUnique()
    {
        var stems = Index.Creations
            .Select(c => CreationIndex.StemOf(c.Plugin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        Assert.Equal(Index.Creations.Count, stems);
    }

    [Fact]
    public void EveryRowIsFilledIn()
    {
        Assert.All(Index.Creations, creation =>
        {
            Assert.True(creation.ContentId > 0, $"'{creation.DisplayName}' has no content id.");
            Assert.False(string.IsNullOrWhiteSpace(creation.Plugin));
            Assert.False(string.IsNullOrWhiteSpace(creation.DisplayName));
            Assert.Equal(creation.DisplayName.Trim(), creation.DisplayName);
        });
    }

    /// <summary>
    ///     Every plugin is a master or a light master. Anything else in this column would mean the
    ///     extraction paired the wrong things up.
    /// </summary>
    [Fact]
    public void EveryPluginIsAPluginFile()
    {
        Assert.All(Index.Creations, creation =>
            Assert.True(creation.Plugin.EndsWith(".esl", StringComparison.OrdinalIgnoreCase) ||
                        creation.Plugin.EndsWith(".esm", StringComparison.OrdinalIgnoreCase),
                $"'{creation.Plugin}' is not a plugin file."));
    }

    [Fact]
    public void ContentIdsAreTheWholeTable()
    {
        Assert.Equal(Index.Creations.Select(c => c.ContentId), Index.ContentIds);
    }

    /// <summary>
    ///     The point of the stem match: a Creation's plugin and its archive are one download, and either of
    ///     them being wanted has to reach the same content id. Case is ignored because the table itself is
    ///     inconsistent about it.
    /// </summary>
    [Theory]
    [InlineData(@"Data\ccBGSSSE002-ExoticArrows.esl", 5648)]
    [InlineData(@"Data\ccBGSSSE002-ExoticArrows.bsa", 5648)]
    [InlineData(@"data/ccbgssse002-exoticarrows.bsa", 5648)]
    [InlineData("CCBGSSSE002-EXOTICARROWS.ESL", 5648)]
    [InlineData(@"Data\ccEEJSSE001-Hstead.esm", 5655)]
    [InlineData(@"Data\ccEEJSSE001-Hstead.bsa", 5655)]
    [InlineData(@"Data\ccbgssse041-netchleather.esl", 5698)]
    [InlineData(@"Data\ccBGSSSE041-NetchLeather.bsa", 5698)]
    public void ResolvesEitherOfACreationsFilesToItsContentId(string path, long expected)
    {
        Assert.True(Index.TryGetContentId(path.ToRelativePath(), out var contentId));
        Assert.Equal(expected, contentId);
    }

    [Theory]
    [InlineData(@"Data\Skyrim.esm")]
    [InlineData(@"Data\Dawnguard.esm")]
    [InlineData("SkyrimSE.exe")]
    [InlineData(@"Data\ccBGSSSE002-ExoticArrowsButNotReally.esl")]
    [InlineData("")]
    public void SaysNothingAboutAFileThatIsNotACreation(string path)
    {
        Assert.False(Index.TryGetContentId(path, out var contentId));
        Assert.Equal(0, contentId);
    }

    /// <summary>A default path asks about no file at all, and must not throw on the way to saying so.</summary>
    [Fact]
    public void SaysNothingAboutNoPathAtAll()
    {
        Assert.Null(Index.Find(default(RelativePath)));
    }

    [Theory]
    [InlineData(@"data\Foo.bsa", "Foo")]
    [InlineData("data/Foo.bsa", "Foo")]
    [InlineData("Foo.bsa", "Foo")]
    [InlineData("Foo", "Foo")]
    [InlineData("Foo.tar.gz", "Foo.tar")]
    [InlineData(".hidden", ".hidden")]
    [InlineData("", "")]
    public void TakesAStemFromTheLeafOfAName(string name, string expected)
    {
        Assert.Equal(expected, CreationIndex.StemOf(name));
    }

    [Fact]
    public void RefusesATableWithTwoCreationsOnOneStem()
    {
        var json = """
            {
              "creations": [
                { "content_id": 1, "plugin": "ccFoo.esl", "display_name": "One" },
                { "content_id": 2, "plugin": "ccFOO.esm", "display_name": "Two" }
              ]
            }
            """;

        Assert.Throws<InvalidDataException>(() => CreationIndex.Load(json));
    }

    [Fact]
    public void RefusesAnEmptyTable()
    {
        Assert.Throws<InvalidDataException>(() => CreationIndex.Load("""{"creations": []}"""));
    }
}
