using System;

namespace Wabbajack.Paths;

/// <summary>A program's file name on this platform: Windows gives it .exe, everywhere else it has none.</summary>
public static class PlatformExecutable
{
    public static string Name(string program)
    {
        return OperatingSystem.IsWindows() ? program + ".exe" : program;
    }
}
