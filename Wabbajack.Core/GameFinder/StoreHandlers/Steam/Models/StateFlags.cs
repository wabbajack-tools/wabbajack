using System;
using JetBrains.Annotations;

namespace Wabbajack.GameFinder.StoreHandlers.Steam.Models;

/// <summary>
/// Describes the various state an app can be in.
/// </summary>
/// <remarks>
/// These values are sourced from https://github.com/lutris/lutris/blob/master/docs/steam.rst.
/// </remarks>
[PublicAPI]
[Flags]
public enum StateFlags : uint
{
    /// <summary>
    /// Invalid.
    /// </summary>
    Invalid = 0,

    /// <summary>
    /// Uninstalled.
    /// </summary>
    Uninstalled = 1,

    /// <summary>
    /// Update Required.
    /// </summary>
    UpdateRequired = 2 << 0,

    /// <summary>
    /// Fully Installed.
    /// </summary>
    FullyInstalled = 2 << 1,

    /// <summary>
    /// Encrypted.
    /// </summary>
    Encrypted = 2 << 2,

    /// <summary>
    /// Locked.
    /// </summary>
    Locked = 2 << 3,

    /// <summary>
    /// Files Missing.
    /// </summary>
    FilesMissing = 2 << 4,

    /// <summary>
    /// App Running.
    /// </summary>
    AppRunning = 2 << 5,

    /// <summary>
    /// Files Corrupt.
    /// </summary>
    FilesCorrupt = 2 << 6,

    /// <summary>
    /// Update Running.
    /// </summary>
    UpdateRunning = 2 << 7,

    /// <summary>
    /// Update Paused.
    /// </summary>
    UpdatePaused = 2 << 8,

    /// <summary>
    /// Update Started.
    /// </summary>
    UpdateStarted = 2 << 9,

    /// <summary>
    /// Uninstalling.
    /// </summary>
    Uninstalling = 2 << 10,

    /// <summary>
    /// Backup Running.
    /// </summary>
    BackupRunning = 2 << 11,

    /// <summary>
    /// Unknown 1.
    /// </summary>
    Unknown1 = 2 << 12,

    /// <summary>
    /// Unknown 2.
    /// </summary>
    Unknown2 = 2 << 13,

    /// <summary>
    /// Unknown 3.
    /// </summary>
    Unknown3 = 2 << 14,

    /// <summary>
    /// Reconfiguring.
    /// </summary>
    Reconfiguring = 2 << 15,

    /// <summary>
    /// Validating.
    /// </summary>
    Validating = 2 << 16,

    /// <summary>
    /// Adding Files.
    /// </summary>
    AddingFiles = 2 << 17,

    /// <summary>
    /// Pre-allocating.
    /// </summary>
    Preallocating = 2 << 18,

    /// <summary>
    /// Downloading.
    /// </summary>
    Downloading = 2 << 19,

    /// <summary>
    /// Staging.
    /// </summary>
    Staging = 2 << 20,

    /// <summary>
    /// Committing.
    /// </summary>
    Committing = 2 << 21,

    /// <summary>
    /// Update Stopping.
    /// </summary>
    UpdateStopping = 2 << 22,
}
