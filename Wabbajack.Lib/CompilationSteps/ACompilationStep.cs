namespace Wabbajack.Lib.CompilationSteps
{
    public abstract class ACompilationStep : ICompilationStep
    {
        protected Compiler _compiler;

        public ACompilationStep(Compiler compiler)
        {
            _compiler = compiler;
        }

        public abstract Directive Run(RawSourceFile source);
        public abstract IState GetState();
    }
}