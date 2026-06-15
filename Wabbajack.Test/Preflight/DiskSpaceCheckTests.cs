// Wabbajack.Test/Preflight/DiskSpaceCheckTests.cs
using Wabbajack.Paths;
using Wabbajack.Preflight;

namespace Wabbajack.Preflight.Test;

public class DiskSpaceCheckTests
{
    [Test]
    public async Task SufficientSpace_Passes()
    {
        var check = new DiskSpaceCheck();
        // Use current drive which should have some free space
        var testPath = (AbsolutePath)AppDomain.CurrentDomain.BaseDirectory;
        var smallSize = 1024L; // 1 KB — should always fit

        check.Update(testPath, testPath, smallSize, smallSize, 0);

        await Assert.That(check.Status).IsEqualTo(PreflightCheckStatus.Passed);
    }

    [Test]
    public async Task InsufficientInstallSpace_Fails()
    {
        var check = new DiskSpaceCheck();
        var testPath = (AbsolutePath)AppDomain.CurrentDomain.BaseDirectory;
        var hugeSize = long.MaxValue / 2;

        check.Update(testPath, testPath, hugeSize, 0, 0);

        await Assert.That(check.Status).IsEqualTo(PreflightCheckStatus.Failed);
        await Assert.That(check.FailureMessage).Contains("disk space", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task AlreadyDownloadedArchives_ReduceRequired()
    {
        var check = new DiskSpaceCheck();
        var testPath = (AbsolutePath)AppDomain.CurrentDomain.BaseDirectory;
        var driveInfo = new DriveInfo(testPath.ToString()[..1]);
        var freeSpace = driveInfo.AvailableFreeSpace;

        // Total archives huge, but already-present covers most of it
        var totalArchiveSize = freeSpace + 1000;
        var alreadyPresent = totalArchiveSize - 100; // only 100 bytes still needed

        check.Update(testPath, testPath, 0, totalArchiveSize, alreadyPresent);

        await Assert.That(check.Status).IsEqualTo(PreflightCheckStatus.Passed);
    }
}
