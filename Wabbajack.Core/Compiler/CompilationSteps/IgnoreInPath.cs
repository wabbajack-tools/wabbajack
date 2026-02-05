using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;
using Wabbajack.Paths;

#pragma warning disable 1998

namespace Wabbajack.Compiler.CompilationSteps;

public class IgnoreInPath : ACompilationStep
{
    private readonly RelativePath _prefix;
    private readonly string _reason;

    public IgnoreInPath(ACompiler compiler, RelativePath prefix) : base(compiler)
    {
        _prefix = prefix;
        _reason = $"Ignored because path starts with {_prefix}";
    }

    public override async ValueTask<Directive?> Run(RawSourceFile source)
    {
        if (!source.Path.InFolder(_prefix))
            return null;

        var result = source.EvolveTo<IgnoredDirectly>();
        result.Reason = _reason;
        return result;
    }
}