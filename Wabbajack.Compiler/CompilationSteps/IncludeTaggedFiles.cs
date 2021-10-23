using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.Paths;

namespace Wabbajack.Compiler.CompilationSteps;

public class IncludeTaggedFiles : ACompilationStep
{
    private readonly RelativePath[] _paths;

    public IncludeTaggedFiles(ACompiler compiler, RelativePath[] paths) : base(compiler)
    {
        _paths = paths;
    }

    public override async ValueTask<Directive?> Run(RawSourceFile source)
    {
        if (!_paths.Any(tag => source.Path.InFolder(tag))) return null;

        var result = source.EvolveTo<InlineFile>();
        result.SourceDataID = await _compiler.IncludeFile(source.AbsolutePath, CancellationToken.None);
        return result;
    }
}