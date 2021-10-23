using System.Threading.Tasks;
using Wabbajack.DTOs;

namespace Wabbajack.Compiler.CompilationSteps;

public abstract class ACompilationStep : ICompilationStep
{
    protected ACompiler _compiler;

    public ACompilationStep(ACompiler compiler)
    {
        _compiler = compiler;
    }

    public abstract ValueTask<Directive?> Run(RawSourceFile source);
}