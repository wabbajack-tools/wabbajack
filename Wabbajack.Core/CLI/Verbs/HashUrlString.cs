using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;

namespace Wabbajack.CLI.Verbs;

public class HashUrlString
{
    private readonly ILogger<HashUrlString> _logger;

    public HashUrlString(ILogger<HashUrlString> logger)
    {
        _logger = logger;
    }

    public static VerbDefinition Definition = new VerbDefinition("hash-url-string",
        "Hashes a URL string and returns the hashcode as hex", new[]
        {
            new OptionDefinition(typeof(AbsolutePath), "u", "url", "Url string to hash")
        });

    public async Task<int> Run(string u)
    {
        _logger.LogInformation("Hash: {Hash}", (await u.Hash()).ToHex());
        return 0;
    }
}