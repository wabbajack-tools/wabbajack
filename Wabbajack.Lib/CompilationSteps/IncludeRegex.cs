using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeRegex : ACompilationStep
    {
        private readonly string _pattern;
        private readonly Regex _regex;

        public IncludeRegex(ACompiler compiler, string pattern) : base(compiler)
        {
            _pattern = pattern;
            _regex = new Regex(pattern);
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            if (!_regex.IsMatch((string)source.Path)) return null;

            var result = source.EvolveTo<InlineFile>();
            result.SourceDataID = await _compiler.IncludeFile(await source.AbsolutePath.ReadAllBytesAsync());
            return result;
        }

        public override IState GetState()
        {
            return new State(_pattern);
        }

        [JsonObject("IncludeRegex")]
        public class State : IState
        {
            public string Pattern { get; set; }

            public State(string pattern)
            {
                Pattern = pattern;
            }

            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IncludeRegex(compiler, Pattern);
            }
        }
    }
}
