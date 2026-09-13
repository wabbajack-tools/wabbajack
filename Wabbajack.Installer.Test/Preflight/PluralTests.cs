using Wabbajack.Installer.Preflight;
using Xunit;

namespace Wabbajack.Installer.Test.Preflight;

public class PluralTests
{
    [Theory]
    [InlineData(0, "0 files")]
    [InlineData(1, "1 file")]
    [InlineData(2, "2 files")]
    public void AppendsAnSUnlessTheCountIsOne(int count, string expected)
    {
        Assert.Equal(expected, Plural.Of(count, "file"));
    }

    [Theory]
    [InlineData(1, "1 archive comes")]
    [InlineData(2, "2 archives come")]
    public void UsesTheGivenPluralForm(int count, string expected)
    {
        Assert.Equal(expected, Plural.Of(count, "archive comes", "archives come"));
    }
}
