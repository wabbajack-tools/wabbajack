using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.Common;
using Wabbajack.Downloaders;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.CLI.Verbs;

public class Extract
{

    private readonly ILogger<DownloadUrl> _logger;
    private readonly FileExtractor.FileExtractor _extractor;

    public Extract(ILogger<DownloadUrl> logger, FileExtractor.FileExtractor extractor)
    {
        _logger = logger;
        _extractor = extractor;
    }

    public static VerbDefinition Definition = new("extract",
        "Extracts the contents of an archive into a folder", new[]
        {
            new OptionDefinition(typeof(AbsolutePath), "i", "input", "Input Archive"),
            new OptionDefinition(typeof(AbsolutePath), "o", "output", "Output Folder")
        });

    internal async Task<int> Run(AbsolutePath input, AbsolutePath output, CancellationToken token)
    {
        if (!output.DirectoryExists())
            output.Parent.CreateDirectory();
        
        await _extractor.ExtractAll(input, output, token, f =>
        {
            Console.WriteLine($" - {f}");
            return true;
        });
        return 0;
    }
}