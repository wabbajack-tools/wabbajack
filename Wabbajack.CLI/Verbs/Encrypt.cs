using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wabbajack.Paths;
using Wabbajack.Paths.IO;
using Wabbajack.Services.OSIntegrated;

namespace Wabbajack.CLI.Verbs;

public class Encrypt : IVerb
{
    private readonly ILogger<Encrypt> _logger;

    public Encrypt(ILogger<Encrypt> logger)
    {
        _logger = logger;
    }

    public Command MakeCommand()
    {
        var command = new Command("encrypt");
        command.Add(new Option<AbsolutePath>(new[] {"-i", "-input"}, "Path to the file to enrypt"));
        command.Add(new Option<string>(new[] {"-n", "-name"}, "Name of the key to store the data into"));
        command.Description = "Encrypts a file and stores it in the Wabbajack encrypted storage";
        command.Handler = CommandHandler.Create(Run);
        return command;
    }

    public async Task<int> Run(AbsolutePath input, string name)
    {
        var data = await input.ReadAllBytesAsync();
        _logger.LogInformation("Encrypting {bytes} bytes into `{key}`", data.Length, name);
        await data.AsEncryptedDataFile(name.ToRelativePath()
            .RelativeTo(KnownFolders.WabbajackAppLocal.Combine("encrypted")));
        return 0;
    }
}