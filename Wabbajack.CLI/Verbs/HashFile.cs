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

    public Command MakeCommand()
    {
        var command = new Command("hash-file");
        command.Add(new Option<AbsolutePath>(new[] {"-i", "-input"}, "Path to the file to hash"));
        command.Description = "Hashes a file with Wabbajack's xxHash64 implementation";
        command.Handler = CommandHandler.Create(Run);
        return command;
    }


    public async Task<int> Run(AbsolutePath input)
    {
        await using var istream = input.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = await istream.HashingCopy(Stream.Null, CancellationToken.None);
        _logger.LogInformation($"{input} hash: {hash} {hash.ToHex()} {(long) hash}");
        return 0;
    }
}