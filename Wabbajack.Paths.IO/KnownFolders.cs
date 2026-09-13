using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Wabbajack.Paths.IO;

public static class KnownFolders
{
    public static AbsolutePath EntryPoint
    {
        get
        {
            return AppDomain.CurrentDomain.BaseDirectory.ToAbsolutePath();
        }
    }

    public static AbsolutePath LauncherAwarePath
    {
        get
        {
            var path = EntryPoint;
            if (path.Depth <= 2) return path;
            if (Version.TryParse(path.Parent.FileName.ToString(), out var version) && version > new Version("1.0.0.0"))
                return path.Parent;
            return path;
        }
    }

    public static bool IsInSpecialFolder(this AbsolutePath candidate, out Environment.SpecialFolder? specialFolder)
    {
        foreach (var val in Enum.GetValues<Environment.SpecialFolder>())
        {
            specialFolder = val;
            AbsolutePath specialPath = Environment.GetFolderPath(val).ToAbsolutePath();
            if ((candidate.ToString().Length > 0 && candidate == specialPath)
                || KnownFolders.IsSubDirectoryOf(candidate.ToString(), specialPath.ToString()))
            {
                return true;
            }
        }
        specialFolder = null;
        return false;
    }
    public static bool IsSubDirectoryOf(this string candidate, string other)
    {
        if (candidate.Length == 0) return false;
        if (other.Length == 0) return false;
        var isChild = false;
        try
        {
            var candidateInfo = new DirectoryInfo(candidate);
            var otherInfo = new DirectoryInfo(other);

            while (candidateInfo.Parent != null)
            {
                if (candidateInfo.Parent.FullName == otherInfo.FullName)
                {
                    isChild = true;
                    break;
                }
                else candidateInfo = candidateInfo.Parent;
            }
        }
        catch (Exception error)
        {
            var message = String.Format("Unable to check directories {0} and {1}: {2}", candidate, other, error);
            Trace.WriteLine(message);
        }

        return isChild;
    }

    public static AbsolutePath AppDataLocal =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).ToAbsolutePath();

    public static AbsolutePath WindowsSystem32 => Environment.GetFolderPath(Environment.SpecialFolder.System).ToAbsolutePath();

    public static AbsolutePath WabbajackAppLocal => AppDataLocal.Combine("Wabbajack");
    public static AbsolutePath CurrentDirectory => Directory.GetCurrentDirectory().ToAbsolutePath();
    public static AbsolutePath Windows => Environment.GetFolderPath(Environment.SpecialFolder.Windows).ToAbsolutePath();

    /// <summary>
    ///     The user's Downloads folder. On Windows this is the shell's known folder, which follows a user who
    ///     has relocated it; elsewhere, and if the shell call fails, it is <c>Downloads</c> under the profile.
    ///     Never throws; the folder may not exist.
    /// </summary>
    public static AbsolutePath Downloads
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                var known = TryGetKnownFolder(FolderIdDownloads);
                if (known != default) return known;
            }

            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(profile))
                profile = Environment.GetEnvironmentVariable("HOME") ?? "";
            if (string.IsNullOrEmpty(profile))
                return EntryPoint.Combine("Downloads");
            return profile.ToAbsolutePath().Combine("Downloads");
        }
    }

    private static readonly Guid FolderIdDownloads = new("374DE290-123F-4565-9164-39C4925E467B");

    private static AbsolutePath TryGetKnownFolder(Guid folderId)
    {
        var buffer = IntPtr.Zero;
        try
        {
            var hr = SHGetKnownFolderPath(folderId, 0, IntPtr.Zero, out buffer);
            if (hr != 0 || buffer == IntPtr.Zero) return default;
            var path = Marshal.PtrToStringUni(buffer);
            return string.IsNullOrWhiteSpace(path) ? default : path.ToAbsolutePath();
        }
        catch (Exception)
        {
            return default;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
                Marshal.FreeCoTaskMem(buffer);
        }
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags,
        IntPtr hToken, out IntPtr ppszPath);
}