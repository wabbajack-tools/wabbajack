namespace Wabbajack.Lib.CompilationSteps
{
    public interface ICompilationStep
    {
        Directive Run(RawSourceFile source);
        IState GetState();
    }

    public interface IState
    {
        ICompilationStep CreateStep(Compiler compiler);
    }
}