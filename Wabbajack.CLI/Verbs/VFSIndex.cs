using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.Paths;
using Wabbajack.VFS;

namespace Wabbajack.CLI.Verbs;

public class VFSIndex : IVerb
{
    private readonly Context _context;

    public VFSIndex(Context context)
    {
        _context = context;
    }

    public static VerbDefinition Definition = new VerbDefinition("vfs-index",
        "Index and cache the contents of a folder", new[]
        {
            new OptionDefinition(typeof(AbsolutePath), "f", "folder", "Folder to index")
        });

    public async Task<int> Run(AbsolutePath folder)
    {
        await _context.AddRoot(folder, CancellationToken.None);
        return 0;
    }
}