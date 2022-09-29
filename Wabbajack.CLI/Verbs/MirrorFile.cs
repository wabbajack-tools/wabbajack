using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;

namespace Wabbajack.CLI.Verbs;

public class MirrorFile : IVerb
{
    private readonly ILogger<MirrorFile> _logger;
    private readonly Client _client;

    public MirrorFile(ILogger<MirrorFile> logger, Client wjClient)
    {
        _logger = logger;
        _client = wjClient;
    }
    public Command MakeCommand()
    {
        var command = new Command("mirror-file");
        command.Add(new Option<AbsolutePath>(new[] {"-i", "-input"}, "File to Mirror"));
        command.Description = "Mirrors a file to the Wabbajack CDN";
        command.Handler = CommandHandler.Create(Run);
        return command;
    }

    public async Task<int> Run(AbsolutePath input)
    {
        _logger.LogInformation("Generating File Definition for {Name}", input.FileName);
        var definition = await _client.GenerateFileDefinition(input);
        await _client.UploadMirror(definition, input);

        return 0;
    }
    
}