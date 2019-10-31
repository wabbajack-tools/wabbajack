using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnoreRegex : ACompilationStep
    {
        private readonly string _reason;
        private readonly Regex _regex;
        private readonly string _pattern;

        public IgnoreRegex(Compiler compiler, string pattern) : base(compiler)
        {
            _pattern = pattern;
            _reason = $"Ignored because path matches regex {pattern}";
            _regex = new Regex(pattern);
        }

        public override Directive Run(RawSourceFile source)
        {
            if (!_regex.IsMatch(source.Path)) return null;
            var result = source.EvolveTo<IgnoredDirectly>();
            result.Reason = _reason;
            return result;
        }

        public override IState GetState()
        {
            return new State(_pattern);
        }

        [JsonObject("IgnorePattern")]
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

            public ICompilationStep CreateStep(Compiler compiler)
            {
                return new IgnoreRegex(compiler, Pattern);
            }
        }
    }
}