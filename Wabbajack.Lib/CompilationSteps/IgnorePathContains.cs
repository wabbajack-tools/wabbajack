using System.Threading.Tasks;
using Newtonsoft.Json;
#nullable enable

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnorePathContains : ACompilationStep
    {
        private readonly string _pattern;
        private readonly string _reason;

        public IgnorePathContains(ACompiler compiler, string pattern) : base(compiler)
        {
            _pattern = $"\\{pattern.Trim('\\')}\\";
            _reason = $"Ignored because path contains {_pattern}";
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            if (!((string)source.Path).Contains(_pattern)) return null;
            var result = source.EvolveTo<IgnoredDirectly>();
            result.Reason = _reason;
            return result;
        }

        public override IState GetState()
        {
            return new State(_pattern);
        }

        [JsonObject("IgnorePathContains")]
        public class State : IState
        {
            public string Pattern { get; set; }

            public State(string pattern)
            {
                Pattern = pattern;
            }

            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IgnorePathContains(compiler, Pattern);
            }
        }
    }
}
