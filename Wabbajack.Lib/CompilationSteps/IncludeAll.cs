using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;
using Wabbajack.Common;

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeAll : ACompilationStep
    {
        public IncludeAll(ACompiler compiler) : base(compiler)
        {
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            var inline = source.EvolveTo<InlineFile>();
            await using var file = await source.File.StagedFile.OpenRead();
            inline.SourceDataID = await _compiler.IncludeFile(await file.ReadAllAsync());
            return inline;
        }

        public override IState GetState()
        {
            return new State();
        }

        [JsonObject("IncludeAll")]
        public class State : IState
        {
            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IncludeAll(compiler);
            }
        }
    }
}
