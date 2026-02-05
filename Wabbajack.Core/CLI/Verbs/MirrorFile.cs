using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.Networking.WabbajackClientApi;
using Wabbajack.Paths;

namespace Wabbajack.CLI.Verbs;

public class MirrorFile
{
    private readonly ILogger<MirrorFile> _logger;
    private readonly Client _client;

    public MirrorFile(ILogger<MirrorFile> logger, Client wjClient)
    {
        _logger = logger;
        _client = wjClient;
    }

    public static VerbDefinition Definition = new("mirror-file", "Mirrors a file to the Wabbajack CDN",
        new[]
        {
            new OptionDefinition(typeof(AbsolutePath), "i", "input", "File to Mirror")
        });
    public async Task<int> Run(AbsolutePath input)
    {
        _logger.LogInformation("Generating File Definition for {Name}", input.FileName);
        var definition = await _client.GenerateFileDefinition(input);
        await _client.UploadMirror(definition, input);

        return 0;
    }
    
}