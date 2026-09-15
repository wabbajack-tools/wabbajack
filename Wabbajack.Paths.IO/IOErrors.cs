using System;
using System.IO;

namespace Wabbajack.Paths.IO;

/// <summary>
///     The Windows error codes that file handling in this tree reacts to, as the HRESULTs .NET puts on the
///     exception (<c>0x8007</c> plus the Win32 code), and the tests that read them.
///     <para>
///         These used to be written out again in each place that needed them — the manual download acquirer,
///         the resumable downloader, and anything deciding whether a failed file operation was worth another
///         attempt — which is how two of those places ended up disagreeing about what "busy" looks like.
///     </para>
/// </summary>
public static class IOErrors
{
    /// <summary>ERROR_FILE_NOT_FOUND.</summary>
    public const int FileNotFound = unchecked((int) 0x80070002);

    /// <summary>ERROR_PATH_NOT_FOUND.</summary>
    public const int PathNotFound = unchecked((int) 0x80070003);

    /// <summary>ERROR_ACCESS_DENIED. Windows uses this for a permission problem, a read-only or directory
    /// destination, and a destination another process holds open, so on its own it says very little.</summary>
    public const int AccessDenied = unchecked((int) 0x80070005);

    /// <summary>ERROR_SHARING_VIOLATION: another process has the file open and will not share it.</summary>
    public const int SharingViolation = unchecked((int) 0x80070020);

    /// <summary>ERROR_LOCK_VIOLATION: a byte range of the file is locked by another process.</summary>
    public const int LockViolation = unchecked((int) 0x80070021);

    /// <summary>ERROR_HANDLE_DISK_FULL.</summary>
    public const int HandleDiskFull = unchecked((int) 0x80070027);

    /// <summary>ERROR_FILE_EXISTS.</summary>
    public const int FileExists = unchecked((int) 0x80070050);

    /// <summary>ERROR_DISK_FULL.</summary>
    public const int DiskFull = unchecked((int) 0x80070070);

    /// <summary>ERROR_INVALID_NAME: a path too long for the volume, or with characters it cannot hold.</summary>
    public const int InvalidName = unchecked((int) 0x8007007B);

    /// <summary>ERROR_ALREADY_EXISTS.</summary>
    public const int AlreadyExists = unchecked((int) 0x800700B7);

    /// <summary>
    ///     The file is open elsewhere. The message test is kept because a stream opened through some shims
    ///     reports the sharing violation in text without the HRESULT.
    /// </summary>
    public static bool IsSharingViolation(Exception ex)
    {
        return ex.HResult is SharingViolation or LockViolation ||
               (ex is IOException && ex.Message.Contains("being used by another process"));
    }

    /// <summary>The volume has no room left.</summary>
    public static bool IsDiskFull(Exception ex)
    {
        return ex.HResult is DiskFull or HandleDiskFull;
    }

    /// <summary>Something is already at the destination and the operation was not allowed to replace it.</summary>
    public static bool IsAlreadyExists(Exception ex)
    {
        return ex.HResult is FileExists or AlreadyExists;
    }

    /// <summary>The path itself is not one the volume can hold, whatever it contains.</summary>
    public static bool IsBadPath(Exception ex)
    {
        return ex is PathTooLongException || ex.HResult == InvalidName;
    }
}
