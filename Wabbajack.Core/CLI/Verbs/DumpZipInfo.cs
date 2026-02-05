using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.Common;
using Wabbajack.Compression.Zip;
using Wabbajack.Downloaders;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.CLI.Verbs;

public class DumpZipInfo
{
    private readonly ILogger<DumpZipInfo> _logger;

    public DumpZipInfo(ILogger<DumpZipInfo> logger)
    {
        _logger = logger;
    }

    public static VerbDefinition Definition = new("dump-zip-info",
        "Dumps the contents of a zip file to the console, for use in debugging wabbajack files",
        new[]
        {
            new OptionDefinition(typeof(AbsolutePath), "i", "input", "Zip file to parse"),
            new OptionDefinition(typeof(bool), "t", "test", "Test extracting each file")
        });

    internal async Task<int> Run(AbsolutePath input, bool test)
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
