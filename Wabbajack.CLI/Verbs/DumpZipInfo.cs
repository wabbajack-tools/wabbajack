using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Compression.Zip;
using Wabbajack.Downloaders;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.CLI.Verbs;

public class DumpZipInfo : IVerb
{
    private readonly ILogger<DumpZipInfo> _logger;

    public DumpZipInfo(ILogger<DumpZipInfo> logger)
    {
        _logger = logger;
    }

    public Command MakeCommand()
    {
        var command = new Command("dump-zip-info");
        command.Add(new Option<AbsolutePath>(new[] {"-i", "-input"}, "Zip file ot parse"));
        command.Add(new Option<bool>(new[] {"-t", "-test"}, "Test extracting each file"));
        command.Description = "Dumps the contents of a zip file to the console, for use in debugging wabbajack files";
        command.Handler = CommandHandler.Create(Run);
        return command;
    }

    private async Task<int> Run(AbsolutePath input, bool test)
    {
        await using var ar = new ZipReader(input.Open(FileMode.Open), false);
        foreach (var value in (await ar.GetFiles()))
        {
            if (test)
            {
                _logger.LogInformation("Extracting {File}", value.FileName);
                await ar.Extract(value, new MemoryStream(), CancellationToken.None);
            }
            else
            {
                
                _logger.LogInformation("Read {File}", value.FileName);
            }
        }

        return 0;

    }
}
