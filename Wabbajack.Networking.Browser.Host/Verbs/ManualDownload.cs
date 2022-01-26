using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Verbs;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.Networking.Browser.Verbs;

public class ManualDownload : AVerb
{
    private readonly ILogger<ManualDownload> _logger;

    public ManualDownload(ILogger<ManualDownload> logger)
    {
        _logger = logger;

    }

    public override Command MakeCommand()
    {
        var command = new Command("manual-download");
        command.Description = "Prompt the user to download a file";
        command.Add(new Option<Uri>(new[] {"-u", "-url"}, "Uri"));
        command.Add(new Option<AbsolutePath>);
        command.Handler = CommandHandler.Create(Run);
        return command;
    }
    
    public async Task<int> Run(Uri url)
    {
        await Browser.WaitForReady();
        await Browser.NavigateTo(url);
        

        await Task.Delay(100000);
        return 0;
    }
}