// Wabbajack.Test/Preflight/PathValidationCheckTests.cs
using Wabbajack.Preflight;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.Preflight.Test;

public class PathValidationCheckTests
{
    [Test]
    public async Task ValidPaths_Pass()
    {
        var check = new PathValidationCheck();
        check.Update(
            (AbsolutePath)@"D:\Modlists\TestList",
            (AbsolutePath)@"D:\Modlists\TestList\downloads",
            Array.Empty<AbsolutePath>());

        await Assert.That(check.Status).IsEqualTo(PreflightCheckStatus.Passed);
    }

    [Test]
    public async Task EmptyInstallPath_Fails()
    {
        var check = new PathValidationCheck();
        check.Update(
            default,
            (AbsolutePath)@"D:\Downloads",
            Array.Empty<AbsolutePath>());

        await Assert.That(check.Status).IsEqualTo(PreflightCheckStatus.Failed);
        await Assert.That(check.FailureMessage).Contains("installation location", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task IdenticalPaths_Fails()
    {
        var check = new PathValidationCheck();
        check.Update(
            (AbsolutePath)@"D:\Modlists\TestList",
            (AbsolutePath)@"D:\Modlists\TestList",
            Array.Empty<AbsolutePath>());

        await Assert.That(check.Status).IsEqualTo(PreflightCheckStatus.Failed);
        await Assert.That(check.FailureMessage).Contains("identical", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task InstallInGameFolder_Fails()
    {
        var gameFolders = new[] { (AbsolutePath)@"C:\Games\Skyrim" };
        var check = new PathValidationCheck();
        check.Update(
            (AbsolutePath)@"C:\Games\Skyrim\mods",
            (AbsolutePath)@"D:\Downloads",
            gameFolders);

        await Assert.That(check.Status).IsEqualTo(PreflightCheckStatus.Failed);
        await Assert.That(check.FailureMessage).Contains("game folder", StringComparison.OrdinalIgnoreCase);
    }
}
