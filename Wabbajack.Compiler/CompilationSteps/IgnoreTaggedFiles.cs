using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.Paths;

namespace Wabbajack.Compiler.CompilationSteps;

public class IgnoreTaggedFiles : ACompilationStep
{
    private readonly RelativePath[] _paths;

    public IgnoreTaggedFiles(ACompiler compiler, RelativePath[] paths) : base(compiler)
    {
        _paths = paths;
    }

    public override async ValueTask<Directive?> Run(RawSourceFile source)
    {
        if (!_paths.Any(tag => source.Path.InFolder(tag))) return null;

        var result = source.EvolveTo<IgnoredDirectly>();
        result.Reason = "Patches ignore path";
        return result;
    }
}