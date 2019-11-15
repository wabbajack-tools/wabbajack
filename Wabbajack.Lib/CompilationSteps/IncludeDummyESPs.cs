using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeDummyESPs : ACompilationStep
    {
        public IncludeDummyESPs(ACompiler compiler) : base(compiler)
        {
        }

        public override Directive Run(RawSourceFile source)
        {
            if (Path.GetExtension(source.AbsolutePath) != ".esp" &&
                Path.GetExtension(source.AbsolutePath) != ".esm") return null;

            var bsa = Path.Combine(Path.GetDirectoryName(source.AbsolutePath),
                Path.GetFileNameWithoutExtension(source.AbsolutePath) + ".bsa");
            var bsaTextures = Path.Combine(Path.GetDirectoryName(source.AbsolutePath),
                Path.GetFileNameWithoutExtension(source.AbsolutePath) + " - Textures.bsa");
            var espSize = new FileInfo(source.AbsolutePath).Length;

            if (espSize > 250 || !File.Exists(bsa) && !File.Exists(bsaTextures)) return null;

            var inline = source.EvolveTo<InlineFile>();
            inline.SourceDataID = _compiler.IncludeFile(File.ReadAllBytes(source.AbsolutePath));
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