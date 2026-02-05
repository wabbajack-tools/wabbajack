using System.Linq;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.DTOs.DownloadStates;
using Wabbajack.VFS;

namespace Wabbajack.Compiler.CompilationSteps;

public class DirectMatch : ACompilationStep
{
    public DirectMatch(ACompiler compiler) : base(compiler)
    {
    }

    public static int GetFilePriority(ACompiler compiler, VirtualFile file)
    {
        var archive = file.TopParent;
        var adata = compiler.ArchivesByFullPath[archive.AbsoluteName];
        if (adata.State is GameFileSource gs) return gs.Game == compiler._settings.Game ? 2 : 3;
        return 1;
    }

    public override async ValueTask<Directive?> Run(RawSourceFile source)
    {
        if (!_compiler.IndexedFiles.TryGetValue(source.Hash, out var found)) return null;
        var result = source.EvolveTo<FromArchive>();

        var match = found.Where(f => f.Name.FileName == source.Path.FileName)
                        .OrderBy(f => GetFilePriority(_compiler, f))
                        .ThenBy(f => f.NestingFactor)
                        .FirstOrDefault()
                    ?? found.OrderBy(f => f.NestingFactor).First();

        result.ArchiveHashPath = match.MakeRelativePaths();

        return result;
    }
}