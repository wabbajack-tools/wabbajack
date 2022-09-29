using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;

namespace Wabbajack.CLI.Verbs;

public class HashUrlString : IVerb
{
    private readonly ILogger<HashUrlString> _logger;

    public HashUrlString(ILogger<HashUrlString> logger)
    {
        _logger = logger;
    }

    public Command MakeCommand()
    {
        var command = new Command("hash-url-string");
        command.Add(new Option<AbsolutePath>(new[] {"-u", "-url"}, "Url string to hash"));
        command.Description = "Hashes a URL string and returns the hashcode as hex";
        command.Handler = CommandHandler.Create(Run);
        return command;
    }


    public async Task<int> Run(string u)
    {
        _logger.LogInformation("Hash: {Hash}", (await u.Hash()).ToHex());
        return 0;
    }
}