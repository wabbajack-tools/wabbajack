using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Paths;
using Wabbajack.VFS;

namespace Wabbajack.CLI.Verbs;

public class VFSIndexFolder : IVerb
{
    private readonly Context _context;

    public VFSIndexFolder(Context context)
    {
        _context = context;
    }

    public Command MakeCommand()
    {
        var command = new Command("vfs-index");
        command.Add(new Option<AbsolutePath>(new[] {"-f", "--folder"}, "Folder to index"));
        command.Description = "Index and cache the contents of a folder";

        command.Handler = CommandHandler.Create(Run);
        return command;
    }

    public async Task<int> Run(AbsolutePath folder)
    {
        await _context.AddRoot(folder, CancellationToken.None);
        return 0;
    }
}