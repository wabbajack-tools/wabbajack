using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnoreEndsWith : ACompilationStep
    {
        private readonly string _postfix;
        private readonly string _reason;

        public IgnoreEndsWith(ACompiler compiler, string postfix) : base(compiler)
        {
            _postfix = postfix;
            _reason = $"Ignored because path ends with {postfix}";
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            if (!((string)source.Path).EndsWith(_postfix)) return null;
            var result = source.EvolveTo<IgnoredDirectly>();
            result.Reason = _reason;
            return result;
        }

        public override IState GetState()
        {
            return new State(_postfix);
        }

        [JsonObject("IgnoreEndsWith")]
        public class State : IState
        {
            public string Postfix { get; set; }

            public State(string postfix)
            {
                Postfix = postfix;
            }

            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IgnoreEndsWith(compiler, Postfix);
            }
        }
    }
}
