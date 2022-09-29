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

    public Command MakeCommand()
    {
        var command = new Command("download-url");
        command.Add(new Option<Uri>(new[] {"-u", "-url"}, "Url to parse"));
        command.Add(new Option<AbsolutePath>(new[] {"-o", "-output"}, "Output file"));
        command.Add(new Option<bool>(new [] {"-p", "--proxy"}, "Use the Wabbajack Proxy (default: true)"));
        command.Description = "Downloads a file to a given output";
        command.Handler = CommandHandler.Create(Run);
        return command;
    }

    private async Task<int> Run(Uri url, AbsolutePath output, bool proxy = true)
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