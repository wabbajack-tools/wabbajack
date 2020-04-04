using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeThisProfile : ACompilationStep
    {
        private readonly IEnumerable<string> _correctProfiles;
        private MO2Compiler _mo2Compiler;

        public IncludeThisProfile(ACompiler compiler) : base(compiler)
        {
            _mo2Compiler = (MO2Compiler) compiler;
            _correctProfiles = _mo2Compiler.SelectedProfiles.Select(p => Path.Combine("profiles", p) + "\\").ToList();
        }

        public override async ValueTask<Directive> Run(RawSourceFile source)
        {
            if (!_correctProfiles.Any(p => source.Path.StartsWith(p)))
                return null;

            var data = source.Path.EndsWith("\\modlist.txt")
                ? ReadAndCleanModlist(source.AbsolutePath)
                : File.ReadAllBytes(source.AbsolutePath);

            var e = source.EvolveTo<InlineFile>();
            e.SourceDataID = _compiler.IncludeFile(data);
            return e;

        }

        public override IState GetState()
        {
            return new State();
        }

        private byte[] ReadAndCleanModlist(string absolutePath)
        {
            var alwaysEnabled = _mo2Compiler.ModInis.Where(f => IgnoreDisabledMods.IsAlwaysEnabled(f.Value))
                .Select(f => f.Key)
                .Distinct();
            var lines = File.ReadAllLines(absolutePath).Where(l =>
            {
                return l.StartsWith("+") 
                       || alwaysEnabled.Any(x => x == l.Substring(1)) 
                       || l.EndsWith("_separator");
            }).ToArray();
            return Encoding.UTF8.GetBytes(string.Join("\r\n", lines));
        }

        [JsonObject("IncludeThisProfile")]
        public class State : IState
        {
            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IncludeThisProfile(compiler);
            }
        }
    }
}
