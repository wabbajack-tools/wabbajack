
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

public class Decrypt
{
    private readonly ILogger<Decrypt> _logger;

    public Decrypt(ILogger<Decrypt> logger)
    {
        _logger = logger;
    }

    public static VerbDefinition Definition = new VerbDefinition("decrypt",
        "Decrypts a file from the wabbajack encrypted storage",
        new[]
        {
            new OptionDefinition(typeof(AbsolutePath), "o", "output", "Output file path"),
            new OptionDefinition(typeof(string), "n", "name", "Name of the key to load data from")
        });
    
    public async Task<int> Run(AbsolutePath output, string name)
    {
        var data = await name.ToRelativePath()
            .RelativeTo(KnownFolders.WabbajackAppLocal.Combine("encrypted"))
            .FromEncryptedDataFile();
        _logger.LogInformation("Decrypting {bytes} bytes into `{key}`", data.Length, name);
        await output.WriteAllBytesAsync(data);

        return 0;
    }
}