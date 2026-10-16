using Xunit;

namespace Wabbajack.Paths.Test;

public class PathCasingTests
{
    [Fact]
    public void LaterSpellingsTakeTheFirstOnesFolders()
    {
        var casing = new PathCasing();
        casing.Unify((RelativePath) @"mods\Armor\Textures\a.dds");

        Assert.Equal(@"mods\Armor\Textures\b.dds", casing.Unify((RelativePath) @"MODS\armor\textures\b.dds").ToString());
    }

    [Fact]
    public void AFileKeepsItsFirstSpellingToo()
    {
        var casing = new PathCasing();
        casing.Unify((RelativePath) @"Data\Skyrim.esm");

        Assert.Equal(@"Data\Skyrim.esm", casing.Unify((RelativePath) @"data\skyrim.esm").ToString());
    }

    [Fact]
    public void OnlyThePartSeenBeforeIsChanged()
    {
        var casing = new PathCasing();
        casing.Unify((RelativePath) @"Stock Game\Data");

        Assert.Equal(@"Stock Game\Data\Strings\New.STRINGS",
            casing.Unify((RelativePath) @"stock game\DATA\Strings\New.STRINGS").ToString());
    }

    [Fact]
    public void FoldersWithTheSameNameInDifferentPlacesAreIndependent()
    {
        var casing = new PathCasing();
        casing.Unify((RelativePath) @"mods\A\Textures\x.dds");

        Assert.Equal(@"mods\B\textures\y.dds", casing.Unify((RelativePath) @"mods\B\textures\y.dds").ToString());
    }

    [Fact]
    public void AnEmptyPathIsLeftAlone()
    {
        Assert.Equal(default, new PathCasing().Unify(default));
    }
}
