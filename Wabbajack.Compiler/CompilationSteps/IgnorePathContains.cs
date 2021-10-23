using System.Threading.Tasks;
using Wabbajack.DTOs;
using Wabbajack.DTOs.Directives;

namespace Wabbajack.Compiler.CompilationSteps;

public class IgnorePathContains : ACompilationStep
{
    private readonly string _pattern;
    private readonly string _reason;

    public IgnorePathContains(ACompiler compiler, string pattern) : base(compiler)
    {
        _pattern = $"\\{pattern.Trim('\\')}\\";
        _reason = $"Ignored because path contains {_pattern}";
    }

    public override async ValueTask<Directive?> Run(RawSourceFile source)
    {
        if (!((string) source.Path).Contains(_pattern)) return null;
        var result = source.EvolveTo<IgnoredDirectly>();
        result.Reason = _reason;
        return result;
    }
}