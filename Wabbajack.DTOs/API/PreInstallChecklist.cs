using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Wabbajack.DTOs.API;

#region Path Validation

/// <summary>
/// Request to validate install and download folder paths.
/// </summary>
public record ValidatePathsRequest(
    [property: JsonPropertyName("installFolder")] string InstallFolder,
    [property: JsonPropertyName("downloadFolder")] string DownloadFolder);

/// <summary>
/// Result of path validation for both install and download folders.
/// </summary>
public record PathValidationResult(
    [property: JsonPropertyName("installFolder")] FolderValidation InstallFolder,
    [property: JsonPropertyName("downloadFolder")] FolderValidation DownloadFolder);

/// <summary>
/// Validation result for a single folder.
/// </summary>
public record FolderValidation(
    [property: JsonPropertyName("isValid")] bool IsValid,
    [property: JsonPropertyName("errors")] List<string> Errors,
    [property: JsonPropertyName("warnings")] List<string> Warnings);

#endregion

#region Game Files Check

/// <summary>
/// Result of checking game files required by the modlist.
/// </summary>
public record GameFilesCheckResult(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("totalFiles")] int TotalFiles,
    [property: JsonPropertyName("checkedFiles")] int CheckedFiles,
    [property: JsonPropertyName("files")] List<GameFileStatus> Files);

/// <summary>
/// Status of a single game file.
/// </summary>
public record GameFileStatus(
    [property: JsonPropertyName("relativePath")] string RelativePath,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("expectedHash")] string ExpectedHash,
    [property: JsonPropertyName("actualHash")] string? ActualHash);

#endregion

#region Manual Downloads Check

/// <summary>
/// Request to check manual downloads.
/// </summary>
public record CheckManualDownloadsRequest(
    [property: JsonPropertyName("downloadFolder")] string DownloadFolder);

/// <summary>
/// Result of checking manual downloads.
/// </summary>
public record ManualDownloadsCheckResult(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("totalFiles")] int TotalFiles,
    [property: JsonPropertyName("foundFiles")] int FoundFiles,
    [property: JsonPropertyName("files")] List<ManualDownloadStatus> Files);

/// <summary>
/// Status of a single manual download.
/// </summary>
public record ManualDownloadStatus(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("expectedSize")] long ExpectedSize,
    [property: JsonPropertyName("expectedHash")] string ExpectedHash,
    [property: JsonPropertyName("foundPath")] string? FoundPath,
    [property: JsonPropertyName("favicon")] string? Favicon);

#endregion

#region Move Download

/// <summary>
/// Request to move a download file to the downloads folder.
/// </summary>
public record MoveDownloadRequest(
    [property: JsonPropertyName("sourcePath")] string SourcePath,
    [property: JsonPropertyName("downloadFolder")] string DownloadFolder);

#endregion

#region Disk Space Check

/// <summary>
/// Request to check disk space.
/// </summary>
public record CheckDiskSpaceRequest(
    [property: JsonPropertyName("installFolder")] string InstallFolder,
    [property: JsonPropertyName("downloadFolder")] string DownloadFolder);

/// <summary>
/// Result of disk space check.
/// </summary>
public record DiskSpaceCheckResult(
    [property: JsonPropertyName("downloadDrive")] DriveSpaceInfo DownloadDrive,
    [property: JsonPropertyName("installDrive")] DriveSpaceInfo InstallDrive,
    [property: JsonPropertyName("areSameDrive")] bool AreSameDrive);

/// <summary>
/// Space information for a drive.
/// </summary>
public record DriveSpaceInfo(
    [property: JsonPropertyName("drivePath")] string DrivePath,
    [property: JsonPropertyName("availableSpace")] long AvailableSpace,
    [property: JsonPropertyName("requiredSpace")] long RequiredSpace,
    [property: JsonPropertyName("hasEnoughSpace")] bool HasEnoughSpace);

#endregion

#region Nexus Login Check

/// <summary>
/// Status of Nexus Mods login.
/// </summary>
public record NexusLoginStatus(
    [property: JsonPropertyName("isLoggedIn")] bool IsLoggedIn,
    [property: JsonPropertyName("username")] string? Username);

#endregion

#region Full Checklist State

/// <summary>
/// Complete state of the pre-install checklist.
/// </summary>
public record PreInstallChecklistState(
    [property: JsonPropertyName("sessionId")] string SessionId,
    [property: JsonPropertyName("pathValidation")] PathValidationResult? PathValidation,
    [property: JsonPropertyName("nexusLogin")] NexusLoginStatus? NexusLogin,
    [property: JsonPropertyName("gameFilesCheck")] GameFilesCheckResult? GameFilesCheck,
    [property: JsonPropertyName("manualDownloadsCheck")] ManualDownloadsCheckResult? ManualDownloadsCheck,
    [property: JsonPropertyName("diskSpaceCheck")] DiskSpaceCheckResult? DiskSpaceCheck,
    [property: JsonPropertyName("canProceed")] bool CanProceed,
    [property: JsonPropertyName("blockingIssues")] List<string> BlockingIssues);

#endregion
