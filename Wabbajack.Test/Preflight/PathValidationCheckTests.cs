// Wabbajack.Test/Preflight/PathValidationCheckTests.cs
using Wabbajack.Preflight;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Xunit;

namespace Wabbajack.Preflight.Test;

public class PathValidationCheckTests
{
    [Fact]
    public void ValidPaths_Pass()
    {
        var check = new PathValidationCheck();
        check.Update(
            (AbsolutePath)@"D:\Modlists\TestList",
            (AbsolutePath)@"D:\Modlists\TestList\downloads",
            Array.Empty<AbsolutePath>());

        Assert.Equal(PreflightCheckStatus.Passed, check.Status);
    }

    [Fact]
    public void EmptyInstallPath_Fails()
    {
        var check = new PathValidationCheck();
        check.Update(
            default,
            (AbsolutePath)@"D:\Downloads",
            Array.Empty<AbsolutePath>());

        Assert.Equal(PreflightCheckStatus.Failed, check.Status);
        Assert.Contains("installation location", check.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IdenticalPaths_Fails()
    {
        var check = new PathValidationCheck();
        check.Update(
            (AbsolutePath)@"D:\Modlists\TestList",
            (AbsolutePath)@"D:\Modlists\TestList",
            Array.Empty<AbsolutePath>());

        Assert.Equal(PreflightCheckStatus.Failed, check.Status);
        Assert.Contains("identical", check.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InstallInGameFolder_Fails()
    {
        var gameFolders = new[] { (AbsolutePath)@"C:\Games\Skyrim" };
        var check = new PathValidationCheck();
        check.Update(
            (AbsolutePath)@"C:\Games\Skyrim\mods",
            (AbsolutePath)@"D:\Downloads",
            gameFolders);

        Assert.Equal(PreflightCheckStatus.Failed, check.Status);
        Assert.Contains("game folder", check.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }
}
