using System.Threading.Tasks;

namespace Wabbajack.Lib.CompilationSteps
{
    public class DropAll : ACompilationStep
    {
        public DropAll(ACompiler compiler) : base(compiler)
        {
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            var result = source.EvolveTo<NoMatch>();
            result.Reason = "No Match in Stack";
            return result;
        }
    }
}
