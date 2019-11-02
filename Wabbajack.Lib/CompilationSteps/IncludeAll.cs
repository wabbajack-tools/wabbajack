using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeAll : ACompilationStep
    {
        public IncludeAll(Compiler compiler) : base(compiler)
        {
        }

        public override Directive Run(RawSourceFile source)
        {
            var inline = source.EvolveTo<InlineFile>();
            inline.SourceDataID = _compiler.IncludeFile(File.ReadAllBytes(source.AbsolutePath));
            return inline;
        }

        public override IState GetState()
        {
            return new State();
        }

        [JsonObject("IncludeAll")]
        public class State : IState
        {
            public ICompilationStep CreateStep(Compiler compiler)
            {
                return new IncludeAll(compiler);
            }
        }
    }
}