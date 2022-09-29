
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.CLI.Verbs;

public class Decrypt : IVerb
{
    private readonly ILogger<Decrypt> _logger;

    public Decrypt(ILogger<Decrypt> logger)
    {
        _logger = logger;
    }

    public Command MakeCommand()
    {
        var command = new Command("decrypt");
        command.Add(new Option<AbsolutePath>(new[] {"-o", "-output"}, "Output file path"));
        command.Add(new Option<string>(new[] {"-n", "-name"}, "Name of the key to load data from"));
        command.Description = "Decrypts a file from the Wabbajack encrypted storage";
        command.Handler = CommandHandler.Create(Run);
        return command;
    }

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