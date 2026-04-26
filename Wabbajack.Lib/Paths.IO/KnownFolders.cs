using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Wabbajack.Paths.IO;

public static class KnownFolders
{
    public static AbsolutePath EntryPoint
    {
        get
        {
            return AppDomain.CurrentDomain.BaseDirectory.ToAbsolutePath();
            var result = Process.GetCurrentProcess().MainModule?.FileName?.ToAbsolutePath() ?? default;

            if (result != default &&
                result.PathParts.Any(p => p.Equals("TestRunner", StringComparison.CurrentCultureIgnoreCase)))
            {
                return Assembly.GetExecutingAssembly().Location.ToAbsolutePath().Parent;
            }
            
            
            if ((result != default && result.Depth > 1 && result.FileName == "dotnet".ToRelativePath())
                || Assembly.GetEntryAssembly() != null)
            {
                result = Assembly.GetEntryAssembly()!.Location.ToAbsolutePath();
            }

            return result == default ? Environment.CurrentDirectory.ToAbsolutePath() : result.Parent;
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
}