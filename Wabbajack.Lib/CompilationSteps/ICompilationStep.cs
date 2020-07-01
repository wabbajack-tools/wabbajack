using System.Threading.Tasks;

namespace Wabbajack.Lib.CompilationSteps
{
    public interface ICompilationStep
    {
        ValueTask<Directive?> Run(RawSourceFile source);
    }
}
