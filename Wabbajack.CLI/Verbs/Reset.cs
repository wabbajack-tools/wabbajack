using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.CLI.Verbs;

public class Reset
{
    private const string WabbajackExecutableName = "Wabbajack.exe";
    private readonly ILogger<Reset> _logger;

    public Reset(ILogger<Reset> logger)
    {
        _logger = logger;
    }

    public static VerbDefinition Definition = new VerbDefinition("reset",
        "Resets Wabbajack settings, restarts the application if open", []);

    public async Task<int> Run()
    {
        Console.WriteLine("Checking if Wabbajack is running...");
        var wabbajackProcess = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(WabbajackExecutableName)).FirstOrDefault();
        string? fileName = wabbajackProcess?.MainModule?.FileName;
        if(wabbajackProcess != null)
        {
            Console.WriteLine("Detected Wabbajack! Killing the process...");
            wabbajackProcess.Kill();
            await wabbajackProcess.WaitForExitAsync();
        }
        Console.WriteLine("Deleting %localappdata%\\Wabbajack...");
        KnownFolders.WabbajackAppLocal.DeleteDirectory();
        if(fileName != null)
        {
            Console.WriteLine("Restarting Wabbajack...");
            Process.Start(fileName);
        }
        Console.WriteLine("Done!");
        return 0;
    }
}