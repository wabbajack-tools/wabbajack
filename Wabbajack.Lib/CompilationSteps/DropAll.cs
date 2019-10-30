using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class DropAll : ACompilationStep
    {
        public DropAll(Compiler compiler) : base(compiler)
        {
        }

        public override Directive Run(RawSourceFile source)
        {
            var result = source.EvolveTo<NoMatch>();
            result.Reason = "No Match in Stack";
            Utils.Log($"No match for: {source.Path}");
            return result;
        }
    }
}
