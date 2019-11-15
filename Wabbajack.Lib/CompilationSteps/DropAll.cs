using Newtonsoft.Json;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class DropAll : ACompilationStep
    {
        public DropAll(ACompiler compiler) : base(compiler)
        {
        }

        public override Directive Run(RawSourceFile source)
        {
            var result = source.EvolveTo<NoMatch>();
            result.Reason = "No Match in Stack";
            Utils.Log($"No match for: {source.Path}");
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