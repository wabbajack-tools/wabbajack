using System.Threading.Tasks;
using Newtonsoft.Json;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class DropAll : ACompilationStep
    {
        public DropAll(ACompiler compiler) : base(compiler)
        {
        }

        public override async ValueTask<Directive> Run(RawSourceFile source)
        {
            var result = source.EvolveTo<NoMatch>();
            result.Reason = "No Match in Stack";
            return result;
        }

        public override IState GetState()
        {
            return new State();
        }

        [JsonObject("DropAll")]
        public class State : IState
        {
            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new DropAll(compiler);
            }
        }
    }
}
