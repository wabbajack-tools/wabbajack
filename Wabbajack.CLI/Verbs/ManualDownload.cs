using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Networking.Browser;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;

namespace Wabbajack.CLI.Verbs;

public class ManualDownload
{
    private readonly ILogger<ManualDownload> _logger;
    private readonly Client _client;
    private readonly IResource<HttpClient> _limiter;

    public ManualDownload(ILogger<ManualDownload> logger, Client client, IResource<HttpClient> limiter)
    {
        _logger = logger;
        _client = client;
        _limiter = limiter;
    }
    public Command MakeCommand()
    {
        var command = new Command("manual-download");
        command.Add(new Option<AbsolutePath>(new[] {"-p", "-prompt"}, "Text prompt to show the user"));
        command.Add(new Option<AbsolutePath>(new[] {"-u", "-url"}, "Uri to show the user"));
        command.Add(new Option<AbsolutePath>(new[] {"-o", "-outputPath"}, "Output Path for the downloaded file"));
        command.Description = "Shows a browser and instructs the user to download a file, exist when the file is downloaded";
        command.Handler = CommandHandler.Create(Run);
        return command;
    }

    public async Task<int> Run(string prompt, Uri url, AbsolutePath outputPath, CancellationToken token)
    {
        _logger.LogInformation("Opening browser");
        using var job = await _limiter.Begin($"Downloading {url}", 0, token);
        await _client.ManualDownload(prompt, url, outputPath, token, job);
        return 0;
    }
}