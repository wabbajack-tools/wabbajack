using System.Threading.Tasks;
using Wabbajack.DTOs;

namespace Wabbajack.Compiler.CompilationSteps;

public interface ICompilationStep
{
    ValueTask<Directive?> Run(RawSourceFile source);
    
    bool Disabled { get; }
}