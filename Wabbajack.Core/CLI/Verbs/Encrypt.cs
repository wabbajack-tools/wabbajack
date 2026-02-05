using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.CLI.Builder;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.CLI.Verbs;

public class Encrypt
{
    private readonly ILogger<Encrypt> _logger;

    public Encrypt(ILogger<Encrypt> logger)
    {
        _logger = logger;
    }

    public static VerbDefinition Definition = new("encrypt",
        "Encrypts a file and stores it in the Wabbajack encrypted storage",
        new[]
        {
            new OptionDefinition(typeof(AbsolutePath), "i", "input", "Path to the file to encrypt"),
            new OptionDefinition(typeof(string), "n", "name", "Name of the key to store the data into")
        });

    internal async Task<int> Run(AbsolutePath input, string name)
    {
        var data = await input.ReadAllBytesAsync();
        _logger.LogInformation("Encrypting {bytes} bytes into `{key}`", data.Length, name);
        await data.AsEncryptedDataFile(name.ToRelativePath()
            .RelativeTo(KnownFolders.WabbajackAppLocal.Combine("encrypted")));
        return 0;
    }
}