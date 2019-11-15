using Newtonsoft.Json;

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

        public override Directive Run(RawSourceFile source)
        {
            if (!source.Path.Contains(_pattern)) return null;
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
            public State()
            {
            }

            public State(string pattern)
            {
                Pattern = pattern;
            }

            public string Pattern { get; set; }

            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IgnorePathContains(compiler, Pattern);
            }
        }
    }
}