using System.Text.RegularExpressions;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IgnoreRegex : ACompilationStep
    {
        private readonly string _reason;
        private string _pattern;
        private readonly Regex _regex;

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
    }
}
