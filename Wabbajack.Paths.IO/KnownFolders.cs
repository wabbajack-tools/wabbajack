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

    public static AbsolutePath AppDataLocal =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).ToAbsolutePath();

    public static AbsolutePath WindowsSystem32 => Environment.GetFolderPath(Environment.SpecialFolder.System).ToAbsolutePath();

    public static AbsolutePath WabbajackAppLocal => AppDataLocal.Combine("Wabbajack");
    public static AbsolutePath CurrentDirectory => Directory.GetCurrentDirectory().ToAbsolutePath();
    public static AbsolutePath Windows => Environment.GetFolderPath(Environment.SpecialFolder.Windows).ToAbsolutePath();
}