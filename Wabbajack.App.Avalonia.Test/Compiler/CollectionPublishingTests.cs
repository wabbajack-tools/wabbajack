using Wabbajack.App.Avalonia.ViewModels.Compiler;
using Xunit;

namespace Wabbajack.App.Avalonia.Test.Compiler;

/// <summary>
///     The compile page's decisions around publishing a list as a Nexus Mods collection: which stored collection a
///     publish updates, what the panel says about the one already there, the game version it asks for, and the
///     page it opens afterwards.
/// </summary>
public class CollectionPublishingTests
{
    [Theory]
    [InlineData("123", "my-slug", "skyrimspecialedition", "skyrimspecialedition", "123", "my-slug")]
    [InlineData("123", "my-slug", "SkyrimSpecialEdition", "skyrimspecialedition", "123", "my-slug")]
    [InlineData("123", "my-slug", null, "skyrimspecialedition", "123", "my-slug")]
    [InlineData("123", "my-slug", "fallout4", "skyrimspecialedition", null, null)]
    [InlineData(null, "my-slug", "skyrimspecialedition", "skyrimspecialedition", null, null)]
    [InlineData("  ", "my-slug", "skyrimspecialedition", "skyrimspecialedition", null, null)]
    public void StoredCollection_IsUpdatedOnlyForTheSameGame(string? id, string? slug, string? storedDomain,
        string listDomain, string? expectedId, string? expectedSlug)
    {
        Assert.Equal((expectedId, expectedSlug), CollectionPublishing.UsableMapping(id, slug, storedDomain, listDomain));
    }

    [Theory]
    [InlineData(4, 2, 4, false)]
    [InlineData(4, null, 4, false)]
    [InlineData(null, 2, 2, true)]
    [InlineData(null, null, null, false)]
    public void ExistingCollection_IsThePublishedRevisionOrTheStoredDraft(int? published, int? stored,
        int? revision, bool draft)
    {
        Assert.Equal((revision, draft), CollectionPublishing.ExistingRevision(published, stored));
    }

    [Theory]
    [InlineData("1.6.1170.0", "1.6.1170.0", "1.6.1170.0")]
    [InlineData("  1.6.1170.0 ", "1.0.0.0", "1.6.1170.0")]
    [InlineData("1.0.0.0", "1.5.97.0", "1.5.97.0")]
    [InlineData("0, 0, 0, 0", "1.5.97.0", "1.5.97.0")]
    [InlineData("", "1.5.97.0", "1.5.97.0")]
    [InlineData(null, "0.0.0.1", null)]
    [InlineData("1.0.0.0", "1, 0, 0, 0", null)]
    public void GameVersion_PrefersProductVersionAndSkipsPlaceholders(string? product, string? file, string? expected)
    {
        Assert.Equal(expected, CollectionPublishing.GameVersion(product, file));
    }

    [Fact]
    public void RevisionPage()
    {
        Assert.Equal("https://www.nexusmods.com/games/skyrimspecialedition/collections/zhhsii/revisions/1",
            CollectionPublishing.RevisionPage("skyrimspecialedition", "zhhsii", 1));
    }
}
