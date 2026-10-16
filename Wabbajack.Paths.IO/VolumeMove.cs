using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Wabbajack.Paths.IO;

/// <summary>
///     A move that is only ever a rename. <see cref="File.Move(string, string, bool)" /> quietly copies and deletes
///     when the two paths are on different volumes, writing the destination a piece at a time, so an interrupted
///     move leaves a partial file where a whole one is expected. Callers that need the destination to appear
///     whole use this, and copy through a temporary name themselves when it declines.
/// </summary>
public static class VolumeMove
{
    private const int Exdev = 18; // "Invalid cross-device link", the same number on Linux and macOS.

    /// <summary>
    ///     Renames <paramref name="src" /> to <paramref name="dest" />, replacing it, and returns true; returns
    ///     false and leaves both alone when they are on different volumes. On Windows that is decided by the
    ///     drive root. Elsewhere every path's root is "/", and a folder can be a symlink or a bind mount onto
    ///     another disk, so the kernel is asked instead: rename(2) refuses to cross file systems.
    /// </summary>
    public static async ValueTask<bool> TryMoveWithinVolumeAsync(this AbsolutePath src, AbsolutePath dest,
        CancellationToken token)
    {
        if (OperatingSystem.IsWindows())
        {
            if (!string.Equals(Path.GetPathRoot(src.ToString()), Path.GetPathRoot(dest.ToString()),
                    StringComparison.OrdinalIgnoreCase))
                return false;
            await src.MoveToAsync(dest, true, token);
            return true;
        }

        token.ThrowIfCancellationRequested();
        if (rename(src.ToString(), dest.ToString()) == 0) return true;

        var errno = Marshal.GetLastPInvokeError();
        if (errno == Exdev) return false;
        throw new IOException($"Could not move {src} to {dest}: {Marshal.GetPInvokeErrorMessage(errno)}", errno);
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int rename([MarshalAs(UnmanagedType.LPUTF8Str)] string oldPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string newPath);
}
