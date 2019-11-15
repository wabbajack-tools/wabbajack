namespace Wabbajack.Lib.CompilationSteps
{
    public abstract class ACompilationStep : ICompilationStep
    {
        protected ACompiler _compiler;

        public ACompilationStep(ACompiler compiler)
        {
            _compiler = compiler;
        }

        public abstract Directive Run(RawSourceFile source);
        public abstract IState GetState();
    }
}