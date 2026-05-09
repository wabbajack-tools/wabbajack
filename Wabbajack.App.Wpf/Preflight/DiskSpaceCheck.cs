// Wabbajack.App.Wpf/Preflight/DiskSpaceCheck.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using Humanizer;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Wabbajack.Paths;

namespace Wabbajack.Preflight;

public partial class DiskSpaceCheck : ReactiveObject, IPreflightCheck
{
    public string Title => "Disk space";
    [Reactive] public partial PreflightCheckStatus Status { get; set; }
    [Reactive] public partial string? FailureMessage { get; set; }
    public ICommand? ActionCommand => null;
    public string? ActionLabel => null;
    public IReadOnlyList<PreflightSubItem>? SubItems => null;

    public DiskSpaceCheck()
    {
        Status = PreflightCheckStatus.Pending;
    }

    public void Update(AbsolutePath installPath, AbsolutePath downloadPath,
        long requiredInstallSize, long totalArchiveSize, long presentArchiveSize)
    {
        var remainingDownloadSize = Math.Max(0, totalArchiveSize - presentArchiveSize);

        try
        {
            var installDrive = new DriveInfo(installPath.ToString()[..1]);
            var downloadDrive = new DriveInfo(downloadPath.ToString()[..1]);
            var sameDrive = installDrive.Name == downloadDrive.Name;

            if (sameDrive)
            {
                var totalNeeded = requiredInstallSize + remainingDownloadSize;
                if (totalNeeded > installDrive.AvailableFreeSpace)
                {
                    Status = PreflightCheckStatus.Failed;
                    FailureMessage = $"Not enough disk space — need {totalNeeded.Bytes()}, only {installDrive.AvailableFreeSpace.Bytes()} free on {installDrive.Name}";
                    return;
                }
            }
            else
            {
                if (requiredInstallSize > installDrive.AvailableFreeSpace)
                {
                    Status = PreflightCheckStatus.Failed;
                    FailureMessage = $"Not enough disk space — install needs {requiredInstallSize.Bytes()}, only {installDrive.AvailableFreeSpace.Bytes()} free on {installDrive.Name}";
                    return;
                }

                if (remainingDownloadSize > downloadDrive.AvailableFreeSpace)
                {
                    Status = PreflightCheckStatus.Failed;
                    FailureMessage = $"Not enough disk space — downloads need {remainingDownloadSize.Bytes()}, only {downloadDrive.AvailableFreeSpace.Bytes()} free on {downloadDrive.Name}";
                    return;
                }
            }

            Status = PreflightCheckStatus.Passed;
            FailureMessage = null;
        }
        catch (Exception ex)
        {
            Status = PreflightCheckStatus.Failed;
            FailureMessage = $"Could not check disk space: {ex.Message}";
        }
    }

    public void Dispose() { }
}
