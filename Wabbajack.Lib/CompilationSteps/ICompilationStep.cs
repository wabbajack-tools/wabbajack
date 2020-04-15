using System.Threading.Tasks;

namespace Wabbajack.Lib.CompilationSteps
{
    public interface ICompilationStep
    {
        ValueTask<Directive?> Run(RawSourceFile source);
        IState GetState();
    }

    public interface IState
    {
        ICompilationStep CreateStep(ACompiler compiler);
    }
}
