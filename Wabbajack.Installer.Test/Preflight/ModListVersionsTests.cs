#nullable enable
using Wabbajack.Installer.Preflight.Rules;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

public class ModListVersionsTests
{
    [Theory]
    [InlineData("1.0.0.0", "2.0.0.0", VersionChange.Major)]
    [InlineData("1.9.9", "2.0.0", VersionChange.Major)]
    [InlineData("1.2.0.0", "1.3.0.0", VersionChange.Minor)]
    [InlineData("1.2.3.0", "1.2.4.0", VersionChange.Patch)]
    [InlineData("1.2.3.4", "1.2.3.5", VersionChange.Patch)]
    [InlineData("2.0.0.0", "2.0.0.0", VersionChange.Same)]
    [InlineData("2.1.0.0", "2.0.0.0", VersionChange.Downgrade)]
    [InlineData("1.0.0.0", "1.0.0.0", VersionChange.Same)]
    public void ComparesVersionParts(string installed, string incoming, VersionChange expected)
    {
        Assert.Equal(expected, ModListVersions.Compare(installed, incoming));
    }

    [Theory]
    [InlineData(null, "1.0.0.0")]
    [InlineData("1.0.0.0", null)]
    [InlineData("", "1.0.0.0")]
    [InlineData("v3-final", "1.0.0.0")]
    [InlineData("1.0.0.0", "not a version")]
    public void AVersionItCannotReadIsUnknown(string? installed, string? incoming)
    {
        Assert.Equal(VersionChange.Unknown, ModListVersions.Compare(installed, incoming));
        Assert.Null(ModListVersions.Describe(VersionChange.Unknown, installed, incoming));
    }

    [Fact]
    public void AMajorChangeWarnsAndSaysItIsNotCertain()
    {
        var text = ModListVersions.Describe(VersionChange.Major, "1.0.0.0", "2.0.0.0");

        Assert.NotNull(text);
        Assert.Contains("likely not save-safe", text);
        Assert.Contains("Not every modlist follows that convention", text);
        Assert.Contains("1.0.0.0 to 2.0.0.0", text);
    }

    [Fact]
    public void ASmallerChangeSaysItUsuallyKeepsSaves()
    {
        var text = ModListVersions.Describe(VersionChange.Minor, "1.2.0.0", "1.3.0.0");

        Assert.NotNull(text);
        Assert.Contains("keeps working with your saves", text);
    }

    [Fact]
    public void ADowngradeIsCalledOut()
    {
        Assert.Contains("older version", ModListVersions.Describe(VersionChange.Downgrade, "2.1", "2.0")!);
    }
}
