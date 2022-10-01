using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Hashing.xxHash64;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;

namespace Wabbajack.CLI.Verbs;

public class HashFile : IVerb
{
    private readonly ILogger<HashFile> _logger;

    public HashFile(ILogger<HashFile> logger)
    {
        _logger = logger;
    }

    public static VerbDefinition Definition = new VerbDefinition("hash-file",
        "Hashes a file with Wabbajack's hashing routines", new[]
        {
            new OptionDefinition(typeof(AbsolutePath), "i", "input", "Path to the file to hash")
        });

    public async Task<int> Run(AbsolutePath input)
    {
        await using var istream = input.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = await istream.HashingCopy(Stream.Null, CancellationToken.None);
        _logger.LogInformation($"{input} hash: {hash} {hash.ToHex()} {(long) hash}");
        return 0;
    }
}