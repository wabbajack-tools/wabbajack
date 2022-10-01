using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Common;
using Wabbajack.Downloaders;
using Wabbajack.DTOs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.CLI.Verbs;

public class DownloadUrl : IVerb
{
    private readonly DownloadDispatcher _dispatcher;
    private readonly ILogger<DownloadUrl> _logger;

    public DownloadUrl(ILogger<DownloadUrl> logger, DownloadDispatcher dispatcher)
    {
        _logger = logger;
        _dispatcher = dispatcher;
    }

    public static VerbDefinition Definition = new VerbDefinition("download-url", "Downloads a file to a given output",
        new[]
        {
            new OptionDefinition(typeof(Uri), "u", "url", "Url to parse"),
            new OptionDefinition(typeof(AbsolutePath), "o", "output", "Output File"),
            new OptionDefinition(typeof(bool), "p", "proxy", "Use the Wabbajack Proxy (default true)")
        });
    
    internal async Task<int> Run(Uri url, AbsolutePath output, bool proxy = true)
    {
        var parsed = _dispatcher.Parse(url);
        if (parsed == null)
        {
            _logger.LogCritical("No downloader found for {Url}", url);

            return 1;
        }

        var archive = new Archive() {State = parsed, Name = output.FileName.ToString()};
        
        var hash = await _dispatcher.Download(archive, output, CancellationToken.None, proxy); ;
        Console.WriteLine($"Download complete: {output.Size().ToFileSizeString()} {hash} {hash.ToHex()} {(long)hash}");
        return 0;
    }
}