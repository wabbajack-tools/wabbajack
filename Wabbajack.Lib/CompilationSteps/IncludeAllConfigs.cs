using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Newtonsoft.Json;
using Wabbajack.Common;
#nullable enable

namespace Wabbajack.Lib.CompilationSteps
{
    public class IncludeAllConfigs : ACompilationStep
    {
        public IncludeAllConfigs(ACompiler compiler) : base(compiler)
        {
        }

        public override async ValueTask<Directive?> Run(RawSourceFile source)
        {
            if (!Consts.ConfigFileExtensions.Contains(source.Path.Extension)) return null;
            var result = source.EvolveTo<InlineFile>();
            result.SourceDataID = await _compiler.IncludeFile(await source.AbsolutePath.ReadAllBytesAsync());
            return result;
        }

        public override IState GetState()
        {
            return new State();
        }

        [JsonObject("IncludeAllConfigs")]
        public class State : IState
        {
            public ICompilationStep CreateStep(ACompiler compiler)
            {
                return new IncludeAllConfigs(compiler);
            }
        }
    }
}
