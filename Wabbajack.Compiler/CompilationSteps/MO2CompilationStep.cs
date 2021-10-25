namespace Wabbajack.Compiler.CompilationSteps;

public abstract class MO2CompilationStep : ACompilationStep
{
    protected MO2CompilationStep(ACompiler compiler) : base(compiler)
    {
    }

    public MO2Compiler MO2Compiler => (MO2Compiler) _compiler;
}