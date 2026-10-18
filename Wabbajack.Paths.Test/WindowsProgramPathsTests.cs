using Xunit;

namespace Wabbajack.Paths.Test;

public class WindowsProgramPathsTests
{
    [Fact]
    public void AWindowsPathIsSpelledAsItIs()
    {
        Assert.Equal(@"C:\Games\Skyrim", ((AbsolutePath) @"C:\Games\Skyrim").ToWindowsProgramPath());
    }

    [Fact]
    public void AUnixPathIsSpelledOnTheWineRootDrive()
    {
        Assert.Equal(@"Z:\home\me\Lists\Skyrim", ((AbsolutePath) "/home/me/Lists/Skyrim").ToWindowsProgramPath());
    }

    [Fact]
    public void AnEmptyPathStaysEmpty()
    {
        Assert.Equal("", default(AbsolutePath).ToWindowsProgramPath());
    }

    [Theory]
    [InlineData(@"Z:\home\me\Downloads")]
    [InlineData("Z:/home/me/Downloads")]
    [InlineData(@"z:\home\me\Downloads")]
    public void AWineRootDrivePathIsAUnixPathOnUnix(string written)
    {
        var path = WindowsProgramPaths.FromWindowsProgramPath(written, true);
        Assert.Equal(PathFormat.Unix, path.PathFormat);
        Assert.Equal("/home/me/Downloads", path.ToString());
    }

    [Fact]
    public void AnotherDriveIsLeftAloneOnUnix()
    {
        // C: is inside a Wine prefix, and which prefix is not something the path says.
        var path = WindowsProgramPaths.FromWindowsProgramPath(@"C:\Games", true);
        Assert.Equal(PathFormat.Windows, path.PathFormat);
    }

    [Fact]
    public void NothingIsTranslatedOnWindows()
    {
        Assert.Equal((AbsolutePath) @"Z:\home\me", WindowsProgramPaths.FromWindowsProgramPath(@"Z:\home\me", false));
    }

    [Fact]
    public void RoundTripsAUnixPath()
    {
        var path = (AbsolutePath) "/mnt/games/Wabbajack/Dust and Death";
        Assert.Equal(path, WindowsProgramPaths.FromWindowsProgramPath(path.ToWindowsProgramPath(), true));
    }
}
