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

public class Restart
{
    private const string WabbajackExecutableName = "Wabbajack.exe";
    private readonly ILogger<Restart> _logger;

    public Restart(ILogger<Restart> logger)
    {
        _logger = logger;
    }

    public static VerbDefinition Definition = new VerbDefinition("restart",
        "Forces the main application to restart when opened", new OptionDefinition[]
        {
        });

    public async Task<int> Run()
    {
        Console.WriteLine("Checking if Wabbajack is running...");
        var wabbajackProcess = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(WabbajackExecutableName)).FirstOrDefault();
        string? fileName = wabbajackProcess?.MainModule?.FileName;
        if(wabbajackProcess != null)
        {
            Console.WriteLine("Detected Wabbajack! Killing the process...");
            wabbajackProcess.Kill();
            Thread.Sleep(500);
        }

        if(fileName != null)
        {
            Console.WriteLine("Restarting Wabbajack...");
            Process.Start(fileName);
        }
        Console.WriteLine("Done!");
        return 0;
    }
}