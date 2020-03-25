using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeDummyESPs : ACompilationStep
    {
        public IncludeDummyESPs(ACompiler compiler) : base(compiler)
        {
        }

        public override async ValueTask<Directive> Run(RawSourceFile source)
        {
            if (source.AbsolutePath.Extension != Consts.ESP &&
                source.AbsolutePath.Extension != Consts.ESM) return null;

            var bsa = source.AbsolutePath.ReplaceExtension(Consts.BSA);
            var bsaTextures = source.AbsolutePath.AppendToName(bsa, " - Textures");

            if (source.AbsolutePath.Size > 250 || !bsa.IsFile && !bsaTextures.IsFile) return null;

            var inline = source.EvolveTo<InlineFile>();
            inline.SourceDataID = await _compiler.IncludeFile(await source.AbsolutePath.ReadAllBytesAsync());
            return inline;
        }

        public override IState GetState()
        {
            return new State();
        }


        [JsonObject("IncludeDummyESPs")]
        public class State : IState
        {
            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IncludeDummyESPs(compiler);
            }
        }
    }
}
