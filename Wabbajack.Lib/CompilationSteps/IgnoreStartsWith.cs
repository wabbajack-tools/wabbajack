using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnoreStartsWith : ACompilationStep
    {
        private readonly string _prefix;
        private readonly string _reason;

        public IgnoreStartsWith(ACompiler compiler, string prefix) : base(compiler)
        {
            _prefix = prefix;
            _reason = string.Format("Ignored because path starts with {0}", _prefix);
        }

        public override async ValueTask<Directive> Run(RawSourceFile source)
        {
            if (!((string)source.Path).StartsWith(_prefix))
            {
                return null;
            }

            var result = source.EvolveTo<IgnoredDirectly>();
            result.Reason = _reason;
            return result;

        }

        public override IState GetState()
        {
            return new State(_prefix);
        }

        [JsonObject("IgnoreStartsWith")]
        public class State : IState
        {
            public State()
            {
            }

            public State(string prefix)
            {
                Prefix = prefix;
            }

            public string Prefix { get; set; }

            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IgnoreStartsWith(compiler, Prefix);
            }
        }
    }
}
