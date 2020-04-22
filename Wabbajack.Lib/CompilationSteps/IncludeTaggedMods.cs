using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeTaggedMods : ACompilationStep
    {
        private readonly IEnumerable<AbsolutePath> _includeDirectly;
        private readonly string _tag;
        private readonly MO2Compiler _mo2Compiler;

        public IncludeTaggedMods(ACompiler compiler, string tag) : base(compiler)
        {
            _mo2Compiler = (MO2Compiler) compiler;
            _tag = tag;
            _includeDirectly = _mo2Compiler.ModInis.Where(kv =>
            {
                var general = kv.Value.General;
                if (general.notes != null && general.notes.Contains(_tag))
                    return true;
                return general.comments != null && general.comments.Contains(_tag);
            }).Select(kv => kv.Key);
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            if (!source.Path.StartsWith(Consts.MO2ModFolderName)) return null;
            foreach (var modpath in _includeDirectly)
            {
                if (!source.AbsolutePath.InFolder(modpath)) continue;
                var result = source.EvolveTo<InlineFile>();
                result.SourceDataID = await _compiler.IncludeFile(source.AbsolutePath);
                return result;
            }

            return null;
        }

        public override IState GetState()
        {
            return new State(_tag);
        }

        [JsonObject("IncludeTaggedMods")]
        public class State : IState
        {
            public string Tag { get; set; }

            public State(string tag)
            {
                Tag = tag;
            }

            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IncludeTaggedMods(compiler, Tag);
            }
        }
    }
}
