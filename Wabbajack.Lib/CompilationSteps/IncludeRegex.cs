using System.Text.RegularExpressions;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeRegex : ACompilationStep
    {
        private readonly string _pattern;
        private readonly Regex _regex;

        public IncludeRegex(Compiler compiler, string pattern) : base(compiler)
        {
            _pattern = pattern;
            _regex = new Regex(pattern);
        }

        public override Directive Run(RawSourceFile source)
        {
            if (!_regex.IsMatch(source.Path)) return null;

            var result = source.EvolveTo<InlineFile>();
            result.SourceDataID = _compiler.IncludeFile(File.ReadAllBytes(source.AbsolutePath));
            return result;
        }

        public override IState GetState()
        {
            return new State(_pattern);
        }

        [JsonObject("IncludeRegex")]
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
                return new IncludeRegex(compiler, Pattern);
            }
        }
    }
}