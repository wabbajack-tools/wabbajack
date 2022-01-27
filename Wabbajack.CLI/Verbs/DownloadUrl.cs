using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Downloaders;
using Wabbajack.DTOs;
using Wabbajack.Paths;

namespace Wabbajack.CLI.Verbs;

public class DownloadUrl : AVerb
{
    private readonly DownloadDispatcher _dispatcher;
    private readonly ILogger<DownloadUrl> _logger;

    public DownloadUrl(ILogger<DownloadUrl> logger, DownloadDispatcher dispatcher)
    {
        _logger = logger;
        _dispatcher = dispatcher;
    }

    public static Command MakeCommand()
    {
        var command = new Command("download-url");
        command.Add(new Option<Uri>(new[] {"-u", "-url"}, "Url to parse"));
        command.Add(new Option<AbsolutePath>(new[] {"-o", "-output"}, "Output file"));
        command.Description = "Downloads a file to a given output";
        return command;
    }

    private async Task<int> Run(Uri url, AbsolutePath output)
    {
        var parsed = _dispatcher.Parse(url);
        if (parsed == null)
        {
            _logger.LogCritical("No downloader found for {Url}", url);

            return 1;
        }

        var archive = new Archive() {State = parsed, Name = output.FileName.ToString()};
        await _dispatcher.Download(archive, output, CancellationToken.None);
        return 0;
    }

    protected override ICommandHandler GetHandler()
    {
        return CommandHandler.Create(Run);
    }
}