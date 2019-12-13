using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeModIniData : ACompilationStep
    {
        public IncludeModIniData(ACompiler compiler) : base(compiler)
        {
        }

        public override async ValueTask<Directive> Run(RawSourceFile source)
        {
            if (!source.Path.StartsWith("mods\\") || !source.Path.EndsWith("\\meta.ini")) return null;
            var e = source.EvolveTo<InlineFile>();
            e.SourceDataID = _compiler.IncludeFile(File.ReadAllBytes(source.AbsolutePath));
            return e;
        }

        public override IState GetState()
        {
            return new State();
        }

        [JsonObject("IncludeModIniData")]
        public class State : IState
        {
            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IncludeModIniData(compiler);
            }
        }
    }
}
