using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Paths;
using Wabbajack.VFS;

namespace Wabbajack.CLI.Verbs;

public class VfsIndexFolder : AVerb
{
    private readonly Context _context;

    public VfsIndexFolder(Context context)
    {
        _context = context;
    }

    public static Command MakeCommand()
    {
        var command = new Command("vfs-index");
        command.Add(new Option<AbsolutePath>(new[] {"-f", "--folder"}, "Folder to index"));
        command.Description = "Index and cache the contents of a folder";
        return command;
    }

    public async Task<int> Run(AbsolutePath folder)
    {
        await _context.AddRoot(folder, CancellationToken.None);
        return 0;
    }

    protected override ICommandHandler GetHandler()
    {
        return CommandHandler.Create(Run);
    }
}